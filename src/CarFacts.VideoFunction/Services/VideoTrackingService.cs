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

    /// <summary>Returns (brand, model) pairs published within the last <paramref name="days"/> days.</summary>
    public async Task<List<(string Brand, string Model)>> GetRecentBrandModelsAsync(int days = 5)
    {
        if (_container == null) return [];
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToString("O");
            var query = new QueryDefinition(
                "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff")
                .WithParameter("@cutoff", cutoff);

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
            _logger.LogInformation("Recent brand+model pairs (last {Days}d): {Count}", days, pairs.Count);
            return pairs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query recent brand+model pairs (non-fatal)");
            return [];
        }
    }

    /// <summary>
    /// Returns all known (brand, model) pairs from the last <paramref name="maxDays"/> days,
    /// deduplicated. Used for Level-2 model-freshness fallback.
    /// </summary>
    public async Task<List<(string Brand, string Model)>> GetAllKnownBrandModelsAsync(int maxDays = 60)
    {
        if (_container == null) return [];
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-maxDays).ToString("O");
            var query = new QueryDefinition(
                "SELECT c.brand, c.model FROM c WHERE c.publishedAt >= @cutoff")
                .WithParameter("@cutoff", cutoff);

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
    /// Returns the brand+model of the entry published longest ago (LRU fallback —
    /// Level 3 when all brands and all models have been used in the last 5 days).
    /// </summary>
    public async Task<(string Brand, string Model)?> GetOldestEntryBrandModelAsync()
    {
        if (_container == null) return null;
        try
        {
            // ORDER BY on publishedAt requires the field to be indexed (it is by default).
            var query = new QueryDefinition(
                "SELECT TOP 1 c.brand, c.model FROM c ORDER BY c.publishedAt ASC");
            var iterator = _container.GetItemQueryIterator<BrandModelRow>(query);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                var row = page.FirstOrDefault();
                if (row != null && !string.IsNullOrWhiteSpace(row.Brand))
                {
                    _logger.LogInformation("Oldest entry (LRU fallback): {Brand} {Model}", row.Brand, row.Model);
                    return (row.Brand, row.Model ?? "");
                }
            }
            return null;
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

    /// <summary>
    /// Returns the best candidate for a "Related Video" backlink:
    /// prefer videos at least 5 days old with the fewest backlinks.
    /// Falls back to the oldest video with fewest backlinks if none are 5 days old.
    /// </summary>
    public async Task<VideoTrackingEntry?> GetRelatedVideoForBacklinkAsync()
    {
        if (_container == null) return null;
        try
        {
            // Fetch up to 20 candidates sorted by backlinkCount (fewest links first).
            // Single-field ORDER BY works without a composite index.
            var query = new QueryDefinition("SELECT TOP 20 * FROM c ORDER BY c.backlinkCount ASC");
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
    public string PublishedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string Brand { get; set; } = "";
    public string? Model { get; set; }
    public string Fact { get; set; } = "";
    public List<string> Keywords { get; set; } = [];
    public int BacklinkCount { get; set; } = 0;
    public string? RelatedVideoId { get; set; }
    public string Platform { get; set; } = "YouTube";
}
