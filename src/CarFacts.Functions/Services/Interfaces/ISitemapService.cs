namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Generates XML sitemap files and writes them to Azure Blob Storage.
/// </summary>
public interface ISitemapService
{
    /// <summary>
    /// Regenerates all sitemap XML files (post-sitemap.xml, news-sitemap.xml,
    /// web-story-sitemap.xml, sitemap_index.xml) from Cosmos DB posts.
    /// Writes the files to Blob Storage in the web-feeds container.
    /// </summary>
    Task RegenerateAllSitemapsAsync(CancellationToken cancellationToken = default);
}
