using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class CosmosPostStore : IPostStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosPostStore> _logger;

    public CosmosPostStore(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosPostStore> logger)
    {
        _container = cosmosClient.GetContainer(settings.Value.DatabaseName, settings.Value.PostsContainerName);
        _logger = logger;
    }

    public async Task SavePostAsync(PostDocument post, CancellationToken cancellationToken = default)
    {
        await _container.UpsertItemAsync(post, new PartitionKey(post.PartitionKey), cancellationToken: cancellationToken);
        _logger.LogInformation("Saved PostDocument id={Id} partitionKey={Pk}", post.Id, post.PartitionKey);
    }

    public async Task<PostDocument?> GetPostAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PostDocument>(id, new PartitionKey(partitionKey), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<PostSummary>> GetAllPostSummariesAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT c.id, c.slug, c.postUrl, c.title, c.metaDescription, c.excerpt, c.featuredImageUrl, c.publishedAt " +
            "FROM c ORDER BY c.publishedAt DESC");

        var results = new List<PostSummary>();
        using var iterator = _container.GetItemQueryIterator<PostSummary>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation("Retrieved {Count} post summaries for sitemap/feed", results.Count);
        return results;
    }

    public async Task<List<PostDocument>> GetRecentPostsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c ORDER BY c.publishedAt DESC OFFSET 0 LIMIT @count")
            .WithParameter("@count", count);

        var results = new List<PostDocument>();
        using var iterator = _container.GetItemQueryIterator<PostDocument>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
