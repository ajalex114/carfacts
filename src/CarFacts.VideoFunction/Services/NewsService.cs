using CarFacts.VideoFunction.Models;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Fetches recent automotive news from free RSS feeds and filters by brand name.
/// All failures are non-fatal — any exception returns null so the LLM path runs.
/// </summary>
public class NewsService(ILogger<NewsService> logger)
{
    private static readonly HttpClient Http;

    static NewsService()
    {
        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        // Some feeds (e.g. Motor Trend) block requests without a browser User-Agent
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
    }

    private static readonly (string Name, string Url)[] Feeds =
    [
        ("Autoblog",       "https://www.autoblog.com/rss.xml"),
        ("Car and Driver", "https://www.caranddriver.com/rss/all.xml/"),
        ("Road and Track", "https://www.roadandtrack.com/rss/all.xml/"),
        ("Autocar",        "https://www.autocar.co.uk/rss"),
    ];

    private static readonly TimeSpan FreshnessThreshold = TimeSpan.FromDays(7);

    /// <summary>
    /// Returns the most recent news article (within 7 days) whose title or description
    /// mentions the given brand, or null if nothing relevant is found.
    /// </summary>
    public async Task<NewsItem?> GetLatestNewsAsync(string brand)
    {
        var brandLower = brand.ToLowerInvariant();
        var cutoff     = DateTimeOffset.UtcNow - FreshnessThreshold;

        // Fetch all feeds in parallel, failures silently produce empty lists
        var feedTasks = Feeds.Select(f => FetchFeedAsync(f.Name, f.Url, brandLower, cutoff));
        var results   = await Task.WhenAll(feedTasks);

        var best = results
            .SelectMany(items => items)
            .OrderByDescending(item => item.PublishedAt)
            .FirstOrDefault();

        if (best != null)
            logger.LogInformation("[NewsService] Found news for '{Brand}': \"{Title}\" ({Source}, {Date:yyyy-MM-dd})",
                brand, best.Title, best.Source, best.PublishedAt);
        else
            logger.LogInformation("[NewsService] No recent news found for '{Brand}' — will use LLM-only path", brand);

        return best;
    }

    private async Task<List<NewsItem>> FetchFeedAsync(
        string sourceName, string feedUrl, string brandLower, DateTimeOffset cutoff)
    {
        try
        {
            var xml = await Http.GetStringAsync(feedUrl);
            var doc = XDocument.Parse(xml);

            var items = doc.Descendants("item")
                .Select(item =>
                {
                    var title   = item.Element("title")?.Value ?? "";
                    var desc    = item.Element("description")?.Value ?? "";
                    var link    = item.Element("link")?.Value ?? "";
                    var pubStr  = item.Element("pubDate")?.Value ?? "";
                    var pubDate = DateTimeOffset.TryParse(pubStr, out var d) ? d : DateTimeOffset.MinValue;

                    return new { title, desc, link, pubDate };
                })
                .Where(i => i.pubDate >= cutoff &&
                            (i.title.ToLowerInvariant().Contains(brandLower) ||
                             i.desc.ToLowerInvariant().Contains(brandLower)))
                .Select(i => new NewsItem(
                    Title:       i.title,
                    Summary:     StripHtml(i.desc),
                    PublishedAt: i.pubDate,
                    Source:      sourceName,
                    Url:         i.link))
                .ToList();

            logger.LogDebug("[NewsService] {Source}: {Count} relevant items", sourceName, items.Count);
            return items;
        }
        catch (Exception ex)
        {
            logger.LogWarning("[NewsService] Failed to fetch {Source}: {Error}", sourceName, ex.Message);
            return [];
        }
    }

    private static string StripHtml(string html)
    {
        // Remove HTML tags and decode common entities for a clean summary
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ").Trim();
        return text.Length > 300 ? text[..300] + "…" : text;
    }
}
