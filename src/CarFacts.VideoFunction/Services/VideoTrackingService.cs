using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Reads and writes published-video tracking entries to Cosmos DB.
/// Uses managed identity (DefaultAzureCredential) — no connection string needed.
/// All operations are non-fatal: if Cosmos is not configured or a call fails,
/// the pipeline continues without interruption.
/// </summary>
public class VideoTrackingService
{
    private readonly Container? _container;
    private readonly ILogger<VideoTrackingService> _logger;

    public VideoTrackingService(string? accountEndpoint, ILogger<VideoTrackingService> logger)
    {
        _logger = logger;
        if (string.IsNullOrWhiteSpace(accountEndpoint))
        {
            _logger.LogWarning("VideoTrackingService: CosmosDB:AccountEndpoint not configured — tracking disabled");
            return;
        }
        var client = new CosmosClient(accountEndpoint, new DefaultAzureCredential(),
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        _container = client.GetContainer("carfacts", "published-videos");
    }

    public async Task SavePublishedVideoAsync(VideoTrackingEntry entry)
    {
        if (_container == null) return;
        try
        {
            await _container.UpsertItemAsync(entry, new PartitionKey(entry.Brand));
            _logger.LogInformation("Tracking saved: jobId={JobId} brand={Brand} yt={YtId}",
                entry.JobId, entry.Brand, entry.YouTubeVideoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save tracking entry (non-fatal)");
        }
    }

    /// <summary>Returns distinct brand names published within the last <paramref name="days"/> days.</summary>
    public async Task<List<string>> GetRecentBrandsAsync(int days = 5)
    {
        if (_container == null) return [];
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToString("O");
            var query = new QueryDefinition(
                "SELECT DISTINCT VALUE c.brand FROM c WHERE c.publishedAt >= @cutoff")
                .WithParameter("@cutoff", cutoff);

            var brands = new List<string>();
            var iterator = _container.GetItemQueryIterator<string>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                brands.AddRange(page.Where(b => !string.IsNullOrWhiteSpace(b)));
            }
            _logger.LogInformation("Recent brands (last {Days}d): {Brands}", days, string.Join(", ", brands));
            return brands;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query recent brands (non-fatal) — returning empty list");
            return [];
        }
    }

    /// <summary>
    /// Returns (brand, model) pairs published within the last <paramref name="days"/> days.
    /// Pass <paramref name="platform"/> to restrict results to a specific platform (e.g. "YouTube" or "Rumble").
    /// Each platform maintains its own independent brand-freshness pool.
    /// </summary>
    public async Task<List<(string Brand, string Model)>> GetRecentBrandModelsAsync(int days = 5, string? platform = null)
    {
        if (_container == null) return [];
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToString("O");
            var sql = string.IsNullOrWhiteSpace(platform)
                ? "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff"
                : "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff AND c.platform = @platform";
            var query = new QueryDefinition(sql).WithParameter("@cutoff", cutoff);
            if (!string.IsNullOrWhiteSpace(platform))
                query = query.WithParameter("@platform", platform);

            var pairs = new List<(string, string)>();
            var iterator = _container.GetItemQueryIterator<BrandModelRow>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 500 });
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                foreach (var row in page)
                    if (!string.IsNullOrWhiteSpace(row.Brand))
                        pairs.Add((row.Brand, row.Model ?? ""));
            }
            _logger.LogInformation("Recent brand+model pairs (last {Days}d, platform={Platform}): {Count}",
                days, platform ?? "all", pairs.Count);
            return pairs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query recent brand+model pairs (non-fatal)");
            return [];
        }
    }

    /// <summary>
    /// Returns all known (brand, model) pairs from the last <paramref name="maxDays"/> days, deduplicated.
    /// Pass <paramref name="platform"/> to restrict to a specific platform's history.
    /// Used for Level-2 model-freshness fallback.
    /// </summary>
    public async Task<List<(string Brand, string Model)>> GetAllKnownBrandModelsAsync(int maxDays = 60, string? platform = null)
    {
        if (_container == null) return [];
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-maxDays).ToString("O");
            var sql = string.IsNullOrWhiteSpace(platform)
                ? "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff"
                : "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff AND c.platform = @platform";
            var query = new QueryDefinition(sql).WithParameter("@cutoff", cutoff);
            if (!string.IsNullOrWhiteSpace(platform))
                query = query.WithParameter("@platform", platform);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pairs = new List<(string, string)>();
            var iterator = _container.GetItemQueryIterator<BrandModelRow>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 500 });
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                foreach (var row in page)
                {
                    if (string.IsNullOrWhiteSpace(row.Brand) || string.IsNullOrWhiteSpace(row.Model)) continue;
                    var key = $"{row.Brand}||{row.Model}".ToLowerInvariant();
                    if (seen.Add(key))
                        pairs.Add((row.Brand, row.Model!));
                }
            }
            return pairs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query all known brand+model pairs (non-fatal)");
            return [];
        }
    }

    /// <summary>
    /// Returns the brand+model for the Level-3 LRU fallback (all brands and all models have
    /// been used recently).
    ///
    /// Bug fix: the old query (SELECT TOP 1 … ORDER BY publishedAt ASC) always returned the
    /// brand from the SINGLE oldest document in the container (Aston Martin in production).
    /// Because new documents are appended without removing old ones, that brand was picked
    /// permanently, causing it to repeat on every video.
    ///
    /// Correct behaviour — per-brand LRU with randomness:
    ///   1. Fetch all (brand, model, publishedAt) entries for the platform.
    ///   2. For each brand, find its most recent publication date (MAX publishedAt).
    ///   3. Sort brands ascending by that max date → least-recently-published brands first.
    ///   4. Take the bottom ~30 % as a candidate pool (min 3, max 10).
    ///   5. Pick randomly from the pool for variety and unpredictability.
    ///
    /// Pass <paramref name="platform"/> to restrict to a specific platform's history.
    /// </summary>
    public async Task<(string Brand, string Model)?> GetOldestEntryBrandModelAsync(string? platform = null)
    {
        if (_container == null) return null;
        try
        {
            var sql = string.IsNullOrWhiteSpace(platform)
                ? "SELECT c.brand, c.model, c.publishedAt FROM c"
                : "SELECT c.brand, c.model, c.publishedAt FROM c WHERE c.platform = @platform";
            var query = new QueryDefinition(sql);
            if (!string.IsNullOrWhiteSpace(platform))
                query = query.WithParameter("@platform", platform);

            var rows = new List<BrandModelDateRow>();
            var iterator = _container.GetItemQueryIterator<BrandModelDateRow>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 500 });
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                rows.AddRange(page.Where(r => !string.IsNullOrWhiteSpace(r.Brand) &&
                                              !string.IsNullOrWhiteSpace(r.PublishedAt)));
            }

            if (rows.Count == 0) return null;

            // Per-brand LRU: for each brand find the most recent publication date.
            var brandLatest = rows
                .GroupBy(r => r.Brand, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(r => r.PublishedAt).First();
                    return (Brand: g.Key, Model: latest.Model ?? "", LatestDate: latest.PublishedAt);
                })
                .OrderBy(x => x.LatestDate)   // ascending = least recently published first
                .ToList();

            // Candidate pool: bottom ~30 % of brands (min 3, max 10).
            var poolSize = Math.Max(3, Math.Min(10, brandLatest.Count / 3));
            var pool     = brandLatest.Take(poolSize).ToList();

            var pick = pool[Random.Shared.Next(pool.Count)];
            _logger.LogInformation(
                "Level3 LRU (per-brand, pool={PoolSize}/{Total}, platform={Platform}): picked {Brand} {Model} (last published: {Date})",
                poolSize, brandLatest.Count, platform ?? "all", pick.Brand, pick.Model, pick.LatestDate);

            return (pick.Brand, pick.Model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query oldest entry (non-fatal)");
            return null;
        }
    }

    private sealed class BrandModelRow
    {
        public string Brand { get; set; } = "";
        public string? Model { get; set; }
    }

    private sealed class BrandModelDateRow
    {
        public string  Brand       { get; set; } = "";
        public string? Model       { get; set; }
        public string? PublishedAt { get; set; }
    }

    /// <summary>
    /// Returns the best candidate for a "Related Video" backlink.
    /// Pass <paramref name="platform"/> to restrict to same-platform videos only.
    /// Prefers videos at least 5 days old with the fewest backlinks.
    /// Falls back to the oldest video with fewest backlinks if none are 5 days old.
    /// </summary>
    public async Task<VideoTrackingEntry?> GetRelatedVideoForBacklinkAsync(string? platform = null)
    {
        if (_container == null) return null;
        try
        {
            var sql = string.IsNullOrWhiteSpace(platform)
                ? "SELECT TOP 20 * FROM c ORDER BY c.backlinkCount ASC"
                : "SELECT TOP 20 * FROM c WHERE c.platform = @platform ORDER BY c.backlinkCount ASC";
            var query = new QueryDefinition(sql);
            if (!string.IsNullOrWhiteSpace(platform))
                query = query.WithParameter("@platform", platform);
            var candidates = new List<VideoTrackingEntry>();
            var iterator = _container.GetItemQueryIterator<VideoTrackingEntry>(query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 20 });
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                candidates.AddRange(page);
            }

            if (candidates.Count == 0) return null;

            // Prefer videos published at least 5 days ago (ISO-8601 strings compare lexicographically).
            var cutoff = DateTimeOffset.UtcNow.AddDays(-5).ToString("O");
            var aged = candidates
                .Where(v => string.Compare(v.PublishedAt, cutoff, StringComparison.Ordinal) <= 0)
                .OrderBy(v => v.BacklinkCount)
                .ThenBy(v => v.PublishedAt)
                .FirstOrDefault();
            if (aged != null)
            {
                _logger.LogInformation("Related video (5d+): {Brand} [{Id}] backlinks={Count}", aged.Brand, aged.Id, aged.BacklinkCount);
                return aged;
            }

            // Fallback: oldest video with smallest backlink count
            var fallback = candidates.OrderBy(v => v.BacklinkCount).ThenBy(v => v.PublishedAt).First();
            _logger.LogInformation("Related video (fallback): {Brand} [{Id}] backlinks={Count}", fallback.Brand, fallback.Id, fallback.BacklinkCount);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get related video (non-fatal)");
            return null;
        }
    }

    /// <summary>Atomically increments the backlinkCount on the tracking entry for the given video.</summary>
    public async Task IncrementBacklinkCountAsync(string id, string brand)
    {
        if (_container == null) return;
        try
        {
            var patchOps = new[] { PatchOperation.Increment("/backlinkCount", 1) };
            await _container.PatchItemAsync<VideoTrackingEntry>(id, new PartitionKey(brand), patchOps);
            _logger.LogInformation("Incremented backlink count for {Id} ({Brand})", id, brand);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment backlink count for {Id} (non-fatal)", id);
        }
    }
}

public class VideoTrackingEntry
{
    public string Id { get; set; } = "";
    public string JobId { get; set; } = "";
    public string? YouTubeVideoId { get; set; }
    public string? YouTubeVideoUrl { get; set; }
    public string? RumbleVideoId { get; set; }
    public string? RumbleVideoUrl { get; set; }
    public string PublishedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string Brand { get; set; } = "";
    public string? Model { get; set; }
    public string Fact { get; set; } = "";
    public string? ImageSearchQuery { get; set; }
    public List<string> Keywords { get; set; } = [];
    public int BacklinkCount { get; set; } = 0;
    public string? RelatedVideoId { get; set; }
    public string Platform { get; set; } = "YouTube";

    /// <summary>Returns the published video URL for whichever platform this entry belongs to.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PlatformVideoUrl => Platform switch
    {
        "Rumble"  => RumbleVideoUrl,
        _         => YouTubeVideoUrl     // YouTube and any future platform default
    };
}
