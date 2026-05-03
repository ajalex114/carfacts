using Azure.Identity;
using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Saves and queries scheduled-video entries in the Cosmos DB <c>video-schedule</c> container.
/// Uses managed identity (DefaultAzureCredential) — no connection string needed.
/// All operations are non-fatal.
/// </summary>
public class VideoScheduleService
{
    private readonly Container? _container;
    private readonly ILogger<VideoScheduleService> _logger;

    public VideoScheduleService(string? accountEndpoint, ILogger<VideoScheduleService> logger)
    {
        _logger = logger;
        if (string.IsNullOrWhiteSpace(accountEndpoint))
        {
            _logger.LogWarning("VideoScheduleService: CosmosDB:AccountEndpoint not configured — schedule storage disabled");
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
        _container = client.GetContainer("carfacts", "video-schedule");
    }

    /// <summary>Upserts all entries for a day in a single batch (best-effort).</summary>
    public async Task SaveScheduleEntriesAsync(List<ScheduleEntry> entries)
    {
        if (_container == null || entries.Count == 0) return;
        foreach (var entry in entries)
        {
            try
            {
                await _container.UpsertItemAsync(entry, new PartitionKey(entry.Platform));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save schedule entry {Id} (non-fatal)", entry.Id);
            }
        }
        _logger.LogInformation("Saved {Count} schedule entries for platform(s) {Platforms}",
            entries.Count, string.Join(", ", entries.Select(e => e.Platform).Distinct()));
    }
}
