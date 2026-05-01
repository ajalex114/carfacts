using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Stores and retrieves full post documents from Cosmos DB.
/// The 'posts' container is the SWA's source of truth for rendering.
/// </summary>
public interface IPostStore
{
    Task SavePostAsync(PostDocument post, CancellationToken cancellationToken = default);

    Task<PostDocument?> GetPostAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>Returns all post summaries ordered by publishedAt descending.</summary>
    Task<List<PostSummary>> GetAllPostSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent N posts (full documents) ordered by publishedAt descending.</summary>
    Task<List<PostDocument>> GetRecentPostsAsync(int count = 20, CancellationToken cancellationToken = default);
}
