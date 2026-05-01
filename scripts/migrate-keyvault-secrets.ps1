#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Migrates Key Vault secrets from the source vault to a target vault
    in a new subscription / tenant.

.DESCRIPTION
    - Grants the running identity temporary "Key Vault Secrets Officer" on source vault
    - Reads all secrets (current versions)
    - Sets each secret in the target vault
    - Revokes the temporary role assignment from source vault when done

.PARAMETER SourceVaultName
    Name of the source Key Vault (old subscription).

.PARAMETER TargetVaultName
    Name of the target Key Vault (new subscription).

.PARAMETER SourceSubscriptionId
    Azure subscription ID of the source vault.

.PARAMETER TargetSubscriptionId
    Azure subscription ID of the target vault.

.PARAMETER DryRun
    Print what would be copied without writing anything.

.EXAMPLE
    .\migrate-keyvault-secrets.ps1 `
        -SourceVaultName "kv-carfacts" `
        -TargetVaultName "kv-carfacts-new" `
        -SourceSubscriptionId "aaaa-..." `
        -TargetSubscriptionId "bbbb-..."
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceVaultName,
    [Parameter(Mandatory)][string]$TargetVaultName,
    [Parameter(Mandatory)][string]$SourceSubscriptionId,
    [Parameter(Mandatory)][string]$TargetSubscriptionId,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command "az" -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI not found. Install from https://aka.ms/installazurecli"
}

# ── Get current user identity for role assignment ────────────────────────────
Write-Host "Getting current identity..." -ForegroundColor Cyan
$identity = az ad signed-in-user show --output json | ConvertFrom-Json
$principalId = $identity.id
Write-Host "  Principal: $($identity.userPrincipalName) ($principalId)" -ForegroundColor Green

# ── Grant temporary Secrets Officer on source vault ───────────────────────────
Write-Host "`nGranting temporary Secrets Officer role on source vault..." -ForegroundColor Cyan
az account set --subscription $SourceSubscriptionId

$sourceVaultId = az keyvault show --name $SourceVaultName --query id -o tsv
$roleAssignmentName = [guid]::NewGuid().ToString()

if (-not $DryRun) {
    az role assignment create `
        --role "Key Vault Secrets Officer" `
        --assignee-object-id $principalId `
        --assignee-principal-type User `
        --scope $sourceVaultId `
        --name $roleAssignmentName | Out-Null
    Write-Host "  Role granted (assignment: $roleAssignmentName)" -ForegroundColor Green
    # Brief wait for RBAC propagation
    Start-Sleep -Seconds 10
} else {
    Write-Host "  [DRY RUN] Would grant Secrets Officer on $SourceVaultName"
}

# ── Read all secrets from source vault ───────────────────────────────────────
Write-Host "`nReading secrets from source vault '$SourceVaultName'..." -ForegroundColor Cyan
$secretList = az keyvault secret list --vault-name $SourceVaultName --output json | ConvertFrom-Json
Write-Host "  Found $($secretList.Count) secrets." -ForegroundColor Green

$results = @()

foreach ($s in $secretList) {
    $name = $s.name
    Write-Host "  Reading '$name'..." -NoNewline

    if (-not $DryRun) {
        $value = az keyvault secret show --vault-name $SourceVaultName --name $name --query value -o tsv
        Write-Host " ✓" -ForegroundColor Green
    } else {
        $value = "<dry-run>"
        Write-Host " [DRY RUN]" -ForegroundColor Yellow
    }

    # ── Write to target vault ────────────────────────────────────────────────
    az account set --subscription $TargetSubscriptionId

    if (-not $DryRun) {
        Write-Host "  Writing '$name' to '$TargetVaultName'..." -NoNewline
        try {
            az keyvault secret set --vault-name $TargetVaultName --name $name --value $value --output none
            Write-Host " ✓" -ForegroundColor Green
            $results += [PSCustomObject]@{ Name=$name; Status="OK" }
        } catch {
            Write-Host " ✗" -ForegroundColor Red
            Write-Warning "    Failed: $_"
            $results += [PSCustomObject]@{ Name=$name; Status="FAILED: $_" }
        }
    } else {
        Write-Host "  [DRY RUN] Would write '$name' to '$TargetVaultName'"
        $results += [PSCustomObject]@{ Name=$name; Status="DRY_RUN" }
    }

    # Switch back to source subscription for next read
    az account set --subscription $SourceSubscriptionId | Out-Null
}

# ── Revoke temporary role from source vault ───────────────────────────────────
Write-Host "`nRevoking temporary role from source vault..." -ForegroundColor Cyan
az account set --subscription $SourceSubscriptionId

if (-not $DryRun) {
    az role assignment delete --ids (
        az role assignment list --scope $sourceVaultId --assignee $principalId `
            --role "Key Vault Secrets Officer" --query "[0].id" -o tsv
    ) --output none
    Write-Host "  Role revoked." -ForegroundColor Green
} else {
    Write-Host "  [DRY RUN] Would revoke Secrets Officer role."
}

# ── Report ─────────────────────────────────────────────────────────────────────
Write-Host "`n────────────────────────────────────" -ForegroundColor Cyan
$ok     = ($results | Where-Object Status -eq "OK").Count
$failed = ($results | Where-Object Status -ne "OK" -and Status -ne "DRY_RUN").Count
Write-Host "Key Vault migration complete." -ForegroundColor Green
Write-Host "  Total  : $($results.Count)"
Write-Host "  Success: $ok"
Write-Host "  Failed : $failed"
$results | Format-Table -AutoSize
Write-Host "────────────────────────────────────" -ForegroundColor Cyan
