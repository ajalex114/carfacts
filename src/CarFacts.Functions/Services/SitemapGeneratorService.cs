using System.Text;
using System.Web;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class SitemapGeneratorService : ISitemapService
{
    private const string SiteBaseUrl = "https://carfactsdaily.com";
    private const int NewsSitemapDaysWindow = 2;

    private readonly IPostStore _postStore;
    private readonly IBlobImageStore _blobStore;
    private readonly ILogger<SitemapGeneratorService> _logger;

    public SitemapGeneratorService(
        IPostStore postStore,
        IBlobImageStore blobStore,
        ILogger<SitemapGeneratorService> logger)
    {
        _postStore = postStore;
        _blobStore = blobStore;
        _logger = logger;
    }

    public async Task RegenerateAllSitemapsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Regenerating all sitemaps...");

        var posts = await _postStore.GetAllPostSummariesAsync(cancellationToken);

        var postSitemapXml = BuildPostSitemap(posts);
        var newsSitemapXml = BuildNewsSitemap(posts);
        var indexXml = BuildSitemapIndex();

        await Task.WhenAll(
            _blobStore.UploadTextFileAsync(postSitemapXml, "post-sitemap.xml", "application/xml", cancellationToken),
            _blobStore.UploadTextFileAsync(newsSitemapXml, "news-sitemap.xml", "application/xml", cancellationToken),
            _blobStore.UploadTextFileAsync(indexXml, "sitemap_index.xml", "application/xml", cancellationToken));

        _logger.LogInformation("Sitemaps regenerated: {PostCount} posts in post-sitemap.xml", posts.Count);
    }

    private static string BuildPostSitemap(List<Models.PostSummary> posts)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"" +
                      " xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        foreach (var post in posts)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{HttpUtility.HtmlEncode(post.PostUrl)}</loc>");
            sb.AppendLine($"    <lastmod>{post.PublishedAt:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>monthly</changefreq>");
            sb.AppendLine("    <priority>0.8</priority>");

            if (!string.IsNullOrEmpty(post.FeaturedImageUrl))
            {
                sb.AppendLine("    <image:image>");
                sb.AppendLine($"      <image:loc>{HttpUtility.HtmlEncode(post.FeaturedImageUrl)}</image:loc>");
                sb.AppendLine($"      <image:title>{HttpUtility.HtmlEncode(post.Title)}</image:title>");
                sb.AppendLine("    </image:image>");
            }

            sb.AppendLine("  </url>");
        }

        sb.Append("</urlset>");
        return sb.ToString();
    }

    private static string BuildNewsSitemap(List<Models.PostSummary> posts)
    {
        var cutoff = DateTime.UtcNow.AddDays(-NewsSitemapDaysWindow);
        var recentPosts = posts.Where(p => p.PublishedAt >= cutoff).ToList();

        var sb = new StringBuilder(1024);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"" +
                      " xmlns:news=\"http://www.google.com/schemas/sitemap-news/0.9\">");

        foreach (var post in recentPosts)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{HttpUtility.HtmlEncode(post.PostUrl)}</loc>");
            sb.AppendLine("    <news:news>");
            sb.AppendLine("      <news:publication>");
            sb.AppendLine("        <news:name>Car Facts Daily</news:name>");
            sb.AppendLine("        <news:language>en</news:language>");
            sb.AppendLine("      </news:publication>");
            sb.AppendLine($"      <news:publication_date>{post.PublishedAt:yyyy-MM-ddTHH:mm:ssZ}</news:publication_date>");
            sb.AppendLine($"      <news:title>{HttpUtility.HtmlEncode(post.Title)}</news:title>");
            sb.AppendLine("    </news:news>");
            sb.AppendLine("  </url>");
        }

        sb.Append("</urlset>");
        return sb.ToString();
    }

    private static string BuildSitemapIndex()
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var sb = new StringBuilder(512);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var name in new[] { "post-sitemap.xml", "news-sitemap.xml" })
        {
            sb.AppendLine("  <sitemap>");
            sb.AppendLine($"    <loc>{SiteBaseUrl}/{name}</loc>");
            sb.AppendLine($"    <lastmod>{now}</lastmod>");
            sb.AppendLine("  </sitemap>");
        }

        sb.Append("</sitemapindex>");
        return sb.ToString();
    }
}
