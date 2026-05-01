#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Migrates Car Facts Daily posts from WordPress.com to Azure Cosmos DB + Blob Storage.

.DESCRIPTION
    1. Exports all published posts from WordPress.com REST API (paginated)
    2. Downloads featured images and re-uploads to Azure Blob Storage (post-images container)
    3. Writes PostDocument records to Cosmos DB (posts container)
    4. Generates a migration report

.PARAMETER WordPressSiteId
    WordPress.com site ID or domain (e.g. carfactsdaily.wordpress.com).

.PARAMETER CosmosEndpoint
    Cosmos DB account endpoint URL.

.PARAMETER CosmosDatabase
    Cosmos DB database name (default: carfacts).

.PARAMETER CosmosContainer
    Cosmos DB container name (default: posts).

.PARAMETER BlobAccountName
    Azure Blob Storage account name for post images.

.PARAMETER BlobContainer
    Blob container name for images (default: post-images).

.PARAMETER SiteBaseUrl
    Canonical site base URL (default: https://carfactsdaily.com).

.PARAMETER DryRun
    If set, prints what would be done without writing anything.

.EXAMPLE
    .\migrate-content.ps1 `
        -WordPressSiteId "carfactsdaily.wordpress.com" `
        -CosmosEndpoint "https://cosmos-carfacts.documents.azure.com:443/" `
        -BlobAccountName "stblobcarfacts"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WordPressSiteId,
    [Parameter(Mandatory)][string]$CosmosEndpoint,
    [string]$CosmosDatabase  = "carfacts",
    [string]$CosmosContainer = "posts",
    [Parameter(Mandatory)][string]$BlobAccountName,
    [string]$BlobContainer   = "post-images",
    [string]$SiteBaseUrl     = "https://carfactsdaily.com",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Dependency checks ─────────────────────────────────────────────────────────
foreach ($cmd in @("az", "azcopy")) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Error "Required tool '$cmd' not found. Install Azure CLI and AzCopy."
    }
}

# ── Auth check ────────────────────────────────────────────────────────────────
Write-Host "Checking Azure login..." -ForegroundColor Cyan
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in. Running 'az login'..."
    az login | Out-Null
    $account = az account show --output json | ConvertFrom-Json
}
Write-Host "  Subscription: $($account.name) ($($account.id))" -ForegroundColor Green

# ── WordPress API helpers ─────────────────────────────────────────────────────
$WP_API = "https://public-api.wordpress.com/wp/v2/sites/$WordPressSiteId"

function Get-WpPosts {
    $page     = 1
    $perPage  = 100
    $allPosts = @()

    do {
        Write-Host "  Fetching WP posts page $page..." -ForegroundColor Gray
        $url  = "$WP_API/posts?status=publish&per_page=$perPage&page=$page&_fields=id,slug,title,date,content,excerpt,featured_media,categories,tags,link"
        $resp = Invoke-RestMethod -Uri $url -ErrorAction SilentlyContinue
        if (-not $resp -or $resp.Count -eq 0) { break }
        $allPosts += $resp
        $page++
    } while ($resp.Count -eq $perPage)

    return $allPosts
}

function Get-WpMedia([int]$mediaId) {
    try {
        return Invoke-RestMethod -Uri "$WP_API/media/$mediaId" -ErrorAction SilentlyContinue
    } catch { return $null }
}

# ── Slug helper ───────────────────────────────────────────────────────────────
function ConvertTo-Slug([string]$text) {
    $slug = $text.ToLowerInvariant()
    $slug = [System.Text.RegularExpressions.Regex]::Replace($slug, '[^a-z0-9\s-]', '')
    $slug = [System.Text.RegularExpressions.Regex]::Replace($slug, '\s+', '-')
    $slug = $slug.TrimStart('-').TrimEnd('-')
    if ($slug.Length -gt 80) { $slug = $slug.Substring(0, 80).TrimEnd('-') }
    return $slug
}

# ── Main migration ────────────────────────────────────────────────────────────
Write-Host "`nFetching posts from WordPress.com..." -ForegroundColor Cyan
$posts = Get-WpPosts
Write-Host "  Found $($posts.Count) published posts." -ForegroundColor Green

$report  = @()
$success = 0
$failed  = 0

foreach ($post in $posts) {
    $date      = [datetime]::Parse($post.date)
    $year      = $date.ToString("yyyy")
    $month     = $date.ToString("MM")
    $day       = $date.ToString("dd")
    $slug      = $post.slug
    $canonical = "$SiteBaseUrl/$year/$month/$day/$slug/"
    $partKey   = "$year-$month"
    $docId     = "$year-$month-$day`_$slug"

    Write-Host "`n[$docId]" -ForegroundColor Yellow

    # ── Image migration ──────────────────────────────────────────────────────
    $blobImageUrl = ""
    if ($post.featured_media -gt 0) {
        $media = Get-WpMedia $post.featured_media
        if ($media -and $media.source_url) {
            $wpImageUrl  = $media.source_url
            $ext         = [System.IO.Path]::GetExtension($wpImageUrl.Split("?")[0])
            $blobPath    = "posts/$year/$month/$day/$slug/featured$ext"
            $blobFullUrl = "https://$BlobAccountName.blob.core.windows.net/$BlobContainer/$blobPath"

            Write-Host "  Image: $wpImageUrl → $blobPath"

            if (-not $DryRun) {
                # Download image to temp file, then upload via AzCopy (uses DefaultAzureCredential)
                $tmpFile = [System.IO.Path]::GetTempFileName() + $ext
                try {
                    Invoke-WebRequest -Uri $wpImageUrl -OutFile $tmpFile -ErrorAction Stop
                    azcopy copy $tmpFile `
                        "https://$BlobAccountName.blob.core.windows.net/$BlobContainer/$blobPath" `
                        --overwrite=ifSourceNewer --log-level=ERROR | Out-Null
                    $blobImageUrl = $blobFullUrl
                    Write-Host "    ✓ Uploaded to Blob Storage" -ForegroundColor Green
                } catch {
                    Write-Warning "    ✗ Image upload failed: $_"
                } finally {
                    if (Test-Path $tmpFile) { Remove-Item $tmpFile -Force }
                }
            } else {
                Write-Host "    [DRY RUN] Would upload image to $blobPath"
                $blobImageUrl = $blobFullUrl
            }
        }
    }

    # ── Build PostDocument ───────────────────────────────────────────────────
    $titleText   = $post.title.rendered -replace '<[^>]+>', ''
    $excerptText = $post.excerpt.rendered -replace '<[^>]+>', ''

    $document = [ordered]@{
        id              = $docId
        partitionKey    = $partKey
        slug            = $slug
        postUrl         = $canonical
        wordPressPostUrl = $post.link
        wordPressPostId  = [string]$post.id
        title           = $titleText
        excerpt         = $excerptText
        htmlContent     = $post.content.rendered
        publishedAt     = $post.date
        images          = @(
            if ($blobImageUrl) {
                @{ factIndex = 0; blobUrl = $blobImageUrl; altText = $titleText }
            }
        )
        keywords        = @()
        facts           = @()
        createdAt       = (Get-Date -Format "o")
        migrated        = $true
    }

    $docJson = $document | ConvertTo-Json -Depth 10 -Compress

    if (-not $DryRun) {
        # Write to Cosmos DB via Azure CLI
        try {
            az cosmosdb sql document create `
                --account-name (($CosmosEndpoint -split "\.")[0] -replace "https://", "") `
                --database-name $CosmosDatabase `
                --container-name $CosmosContainer `
                --partition-key-value $partKey `
                --body $docJson `
                --output none 2>&1 | Out-Null
            Write-Host "  ✓ Saved to Cosmos DB" -ForegroundColor Green
            $success++
            $report += [PSCustomObject]@{ Id=$docId; Slug=$slug; Status="OK"; Image=$blobImageUrl }
        } catch {
            Write-Warning "  ✗ Cosmos write failed: $_"
            $failed++
            $report += [PSCustomObject]@{ Id=$docId; Slug=$slug; Status="COSMOS_FAILED"; Image=$blobImageUrl }
        }
    } else {
        Write-Host "  [DRY RUN] Would write document to Cosmos DB: $docId"
        $success++
        $report += [PSCustomObject]@{ Id=$docId; Slug=$slug; Status="DRY_RUN"; Image=$blobImageUrl }
    }
}

# ── Report ────────────────────────────────────────────────────────────────────
$reportPath = "migration-report-$(Get-Date -Format 'yyyyMMdd-HHmm').csv"
$report | Export-Csv -Path $reportPath -NoTypeInformation
Write-Host "`n────────────────────────────────────" -ForegroundColor Cyan
Write-Host "Migration complete." -ForegroundColor Green
Write-Host "  Posts processed : $($posts.Count)"
Write-Host "  Success         : $success"
Write-Host "  Failed          : $failed"
Write-Host "  Report          : $reportPath"
Write-Host "────────────────────────────────────" -ForegroundColor Cyan
