using System.Text;
using System.Web;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

public sealed class RssFeedGeneratorService : IRssFeedService
{
    private const string SiteBaseUrl = "https://carfactsdaily.com";
    private const string FeedBlobPath = "feed/rss.xml";
    private const int RecentPostCount = 20;

    private readonly IPostStore _postStore;
    private readonly IBlobImageStore _blobStore;
    private readonly ILogger<RssFeedGeneratorService> _logger;

    public RssFeedGeneratorService(
        IPostStore postStore,
        IBlobImageStore blobStore,
        ILogger<RssFeedGeneratorService> logger)
    {
        _postStore = postStore;
        _blobStore = blobStore;
        _logger = logger;
    }

    public async Task RegenerateFeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Regenerating RSS feed...");

        var posts = await _postStore.GetRecentPostsAsync(RecentPostCount, cancellationToken);
        var xml = BuildRssXml(posts);

        await _blobStore.UploadTextFileAsync(xml, FeedBlobPath, "application/rss+xml", cancellationToken);

        _logger.LogInformation("RSS feed regenerated with {Count} posts", posts.Count);
    }

    private static string BuildRssXml(List<Models.PostDocument> posts)
    {
        var sb = new StringBuilder(8192);
        var buildDate = DateTime.UtcNow.ToString("R");

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\"" +
                      " xmlns:media=\"http://search.yahoo.com/mrss/\"" +
                      " xmlns:content=\"http://purl.org/rss/1.0/modules/content/\">");
        sb.AppendLine("  <channel>");
        sb.AppendLine("    <title>Car Facts Daily</title>");
        sb.AppendLine($"    <link>{SiteBaseUrl}</link>");
        sb.AppendLine("    <description>Daily car facts and automotive knowledge. Learn something new about cars every day.</description>");
        sb.AppendLine("    <language>en-us</language>");
        sb.AppendLine($"    <lastBuildDate>{buildDate}</lastBuildDate>");
        sb.AppendLine($"    <atom:link href=\"{SiteBaseUrl}/feed/\" rel=\"self\" type=\"application/rss+xml\"/>");
        sb.AppendLine("    <image>");
        sb.AppendLine("      <url>https://carfactsdaily.com/og-image.png</url>");
        sb.AppendLine("      <title>Car Facts Daily</title>");
        sb.AppendLine($"      <link>{SiteBaseUrl}</link>");
        sb.AppendLine("    </image>");

        foreach (var post in posts)
        {
            var pubDate = post.PublishedAt.ToString("R");
            var escapedTitle = HttpUtility.HtmlEncode(post.Title);
            var escapedDesc = HttpUtility.HtmlEncode(post.Excerpt.Length > 0 ? post.Excerpt : post.MetaDescription);
            var escapedUrl = HttpUtility.HtmlEncode(post.PostUrl);
            var guid = post.PostUrl;

            sb.AppendLine("    <item>");
            sb.AppendLine($"      <title>{escapedTitle}</title>");
            sb.AppendLine($"      <link>{escapedUrl}</link>");
            sb.AppendLine($"      <description>{escapedDesc}</description>");
            sb.AppendLine($"      <pubDate>{pubDate}</pubDate>");
            sb.AppendLine($"      <guid isPermaLink=\"true\">{escapedUrl}</guid>");

            // Categories
            foreach (var keyword in post.Keywords.Take(5))
                sb.AppendLine($"      <category>{HttpUtility.HtmlEncode(keyword)}</category>");

            // Featured image as media:thumbnail
            if (!string.IsNullOrEmpty(post.FeaturedImageUrl))
            {
                var escapedImg = HttpUtility.HtmlEncode(post.FeaturedImageUrl);
                sb.AppendLine($"      <media:thumbnail url=\"{escapedImg}\"/>");
                sb.AppendLine($"      <media:content url=\"{escapedImg}\" medium=\"image\"/>");
            }

            // Full content in content:encoded
            if (!string.IsNullOrEmpty(post.HtmlContent))
            {
                sb.AppendLine($"      <content:encoded><![CDATA[{post.HtmlContent}]]></content:encoded>");
            }

            sb.AppendLine("    </item>");
        }

        sb.AppendLine("  </channel>");
        sb.Append("</rss>");

        return sb.ToString();
    }
}
