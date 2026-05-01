using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;

namespace CarFacts.Functions.Services;

/// <summary>
/// No-op IPostStore for local development when Cosmos DB is not configured.
/// </summary>
public sealed class NullPostStore : IPostStore
{
    public Task SavePostAsync(PostDocument post, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<PostDocument?> GetPostAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
        => Task.FromResult<PostDocument?>(null);

    public Task<List<PostSummary>> GetAllPostSummariesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<PostSummary>());

    public Task<List<PostDocument>> GetRecentPostsAsync(int count = 20, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<PostDocument>());
}
