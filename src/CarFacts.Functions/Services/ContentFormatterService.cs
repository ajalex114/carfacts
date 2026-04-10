using System.Text;
using System.Web;
using CarFacts.Functions.Helpers;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;

namespace CarFacts.Functions.Services;

public sealed class ContentFormatterService : IContentFormatterService
{
    public string FormatPostHtml(CarFactsResponse response, List<UploadedMedia> media, string todayDate)
    {
        var sb = new StringBuilder(8192);

        AppendGeoHeader(sb, response, todayDate);
        AppendTableOfContents(sb, response.Facts);
        AppendFactSections(sb, response.Facts, media);
        AppendConclusion(sb, response.Facts, todayDate);
        AppendFaqSection(sb, response.Facts, todayDate);

        return sb.ToString();
    }

    public string FormatPostHtml(RawCarFactsContent content, SeoMetadata seo, List<UploadedMedia> media, string todayDate, List<BacklinkSuggestion>? backlinks = null, List<RelatedPostSuggestion>? relatedPosts = null)
    {
        // Bridge: compose a CarFactsResponse from the split models
        var response = new CarFactsResponse
        {
            MainTitle = seo.MainTitle,
            MetaDescription = seo.MetaDescription,
            Keywords = seo.Keywords,
            GeoSummary = seo.GeoSummary,
            SocialMediaTeaser = seo.SocialMediaTeaser,
            SocialMediaHashtags = seo.SocialMediaHashtags,
            Facts = content.Facts
        };

        var sb = new StringBuilder(8192);

        AppendGeoHeader(sb, response, todayDate);
        AppendTableOfContents(sb, response.Facts);
        AppendFactSectionsWithBacklinks(sb, response.Facts, media, backlinks ?? []);
        AppendConclusion(sb, response.Facts, todayDate);

        if (relatedPosts is not null && relatedPosts.Count > 0)
            AppendRelatedPostsSection(sb, relatedPosts);
        else
            AppendFaqSection(sb, response.Facts, todayDate);

        return sb.ToString();
    }

    private static void AppendGeoHeader(StringBuilder sb, CarFactsResponse response, string todayDate)
    {
        var escapedTitle = HttpUtility.HtmlEncode(response.MainTitle);
        var escapedDesc = HttpUtility.HtmlEncode(response.MetaDescription);
        var keywords = HttpUtility.HtmlEncode(string.Join(", ", response.Keywords));

        sb.AppendLine($"<!-- GEO Summary for AI Search Engines: {HttpUtility.HtmlEncode(response.GeoSummary)} -->");
        sb.AppendLine($"""<div class="car-facts-intro" itemscope itemtype="https://schema.org/Article">""");
        sb.AppendLine($"""<meta itemprop="headline" content="{escapedTitle}"/>""");
        sb.AppendLine($"""<meta itemprop="description" content="{escapedDesc}"/>""");
        sb.AppendLine($"""<meta itemprop="datePublished" content="{DateTime.UtcNow:O}"/>""");
        sb.AppendLine($"""<meta itemprop="keywords" content="{keywords}"/>""");
        sb.AppendLine($"<p><strong>🚗 On this day in automotive history</strong> — {HttpUtility.HtmlEncode(todayDate)} — " +
            "here are five wild moments that shaped the car world as we know it. " +
            "Buckle up, these are pretty cool.</p>");
        sb.AppendLine("</div>");
        sb.AppendLine();
    }

    private static void AppendTableOfContents(StringBuilder sb, List<CarFact> facts)
    {
        sb.AppendLine("""<div class="table-of-contents" style="background: #f8f9fa; padding: 20px; border-left: 4px solid #007bff; margin: 20px 0;">""");
        sb.AppendLine("<h2>📋 Quick Navigation</h2>");
        sb.AppendLine("<ol>");

        foreach (var (fact, idx) in facts.Select((f, i) => (f, i)))
        {
            sb.AppendLine($"""<li><a href="#{SlugHelper.GenerateAnchorId(fact)}">{HttpUtility.HtmlEncode(fact.CatchyTitle)}</a></li>""");
        }

        sb.AppendLine("</ol>");
        sb.AppendLine("</div>");
        sb.AppendLine();
    }

    public string FormatPostHtmlWithBase64Images(CarFactsResponse response, List<GeneratedImage> images, string todayDate)
    {
        var sb = new StringBuilder(8192);

        AppendGeoHeader(sb, response, todayDate);
        AppendTableOfContents(sb, response.Facts);
        AppendFactSectionsWithBase64(sb, response.Facts, images);
        AppendConclusion(sb, response.Facts, todayDate);
        AppendFaqSection(sb, response.Facts, todayDate);

        return sb.ToString();
    }

    private static void AppendFactSectionsWithBase64(StringBuilder sb, List<CarFact> facts, List<GeneratedImage> images)
    {
        foreach (var (fact, idx) in facts.Select((f, i) => (f, i)))
        {
            var image = images.FirstOrDefault(img => img.FactIndex == idx);
            AppendSingleFactWithBase64(sb, fact, image, idx);
        }
    }

    private static void AppendSingleFactWithBase64(StringBuilder sb, CarFact fact, GeneratedImage? image, int index)
    {
        var escapedTitle = HttpUtility.HtmlEncode(fact.CatchyTitle);
        var escapedModel = HttpUtility.HtmlEncode(fact.CarModel);

        sb.AppendLine($"""<div class="car-fact-section" id="{SlugHelper.GenerateAnchorId(fact)}" itemscope itemtype="https://schema.org/NewsArticle">""");
        sb.AppendLine($"""<h2 itemprop="headline">🏆 {escapedTitle}</h2>""");
        sb.AppendLine($"""<p class="fact-year" style="color: #666; font-style: italic; margin: 10px 0;"><strong>Year:</strong> {fact.Year} | <strong>Vehicle:</strong> {escapedModel}</p>""");

        if (image is not null)
            AppendBase64Image(sb, fact, image);

        sb.AppendLine("""<div itemprop="articleBody">""");
        sb.AppendLine($"<p>{HttpUtility.HtmlEncode(fact.Fact)}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("""<div class="impact-section" style="background: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0;">""");
        sb.AppendLine("<p><strong>💡 The Big Deal:</strong> <em>This one changed the game — it reshaped how we think about cars and set the stage for everything that came after.</em></p>");
        sb.AppendLine("</div>");
        sb.AppendLine();
    }

    private static void AppendBase64Image(StringBuilder sb, CarFact fact, GeneratedImage image)
    {
        var alt = HttpUtility.HtmlEncode($"{fact.CarModel} from {fact.Year} - historic automotive moment");
        var base64 = Convert.ToBase64String(image.ImageData);

        sb.AppendLine("""<figure class="wp-block-image size-large" itemprop="image" itemscope itemtype="https://schema.org/ImageObject">""");
        sb.AppendLine($"""<img src="data:image/png;base64,{base64}" alt="{alt}" style="max-width: 100%; height: auto;" itemprop="url"/>""");
        sb.AppendLine($"""<figcaption itemprop="caption">{HttpUtility.HtmlEncode(fact.CarModel)} ({fact.Year})</figcaption>""");
        sb.AppendLine("</figure>");
    }

    private static void AppendFactSections(StringBuilder sb, List<CarFact> facts, List<UploadedMedia> media)
    {
        foreach (var (fact, idx) in facts.Select((f, i) => (f, i)))
        {
            var image = media.FirstOrDefault(m => m.FactIndex == idx);
            AppendSingleFact(sb, fact, image, idx, null);
        }
    }

    private static void AppendFactSectionsWithBacklinks(StringBuilder sb, List<CarFact> facts, List<UploadedMedia> media, List<BacklinkSuggestion> backlinks)
    {
        foreach (var (fact, idx) in facts.Select((f, i) => (f, i)))
        {
            var image = media.FirstOrDefault(m => m.FactIndex == idx);
            var backlink = backlinks.FirstOrDefault(b => b.FactIndex == idx);
            AppendSingleFact(sb, fact, image, idx, backlink);
        }
    }

    private static void AppendSingleFact(StringBuilder sb, CarFact fact, UploadedMedia? image, int index, BacklinkSuggestion? backlink)
    {
        var escapedTitle = HttpUtility.HtmlEncode(fact.CatchyTitle);
        var escapedModel = HttpUtility.HtmlEncode(fact.CarModel);

        sb.AppendLine($"""<div class="car-fact-section" id="{SlugHelper.GenerateAnchorId(fact)}" itemscope itemtype="https://schema.org/NewsArticle">""");
        sb.AppendLine($"""<h2 itemprop="headline">🏆 {escapedTitle}</h2>""");
        sb.AppendLine($"""<p class="fact-year" style="color: #666; font-style: italic; margin: 10px 0;"><strong>Year:</strong> {fact.Year} | <strong>Vehicle:</strong> {escapedModel}</p>""");

        if (image is not null)
            AppendImage(sb, fact, image);

        sb.AppendLine("""<div itemprop="articleBody">""");
        sb.AppendLine($"<p>{HttpUtility.HtmlEncode(fact.Fact)}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("""<div class="impact-section" style="background: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0;">""");
        sb.AppendLine("<p><strong>💡 The Big Deal:</strong> <em>This one changed the game — it reshaped how we think about cars and set the stage for everything that came after.</em></p>");
        sb.AppendLine("</div>");

        if (backlink is not null)
            AppendBacklink(sb, backlink);

        sb.AppendLine("</div>");
        sb.AppendLine();
    }

    private static void AppendImage(StringBuilder sb, CarFact fact, UploadedMedia image)
    {
        var alt = HttpUtility.HtmlEncode($"{fact.CarModel} from {fact.Year} - historic automotive moment");

        sb.AppendLine("""<figure class="wp-block-image size-large" itemprop="image" itemscope itemtype="https://schema.org/ImageObject">""");
        sb.AppendLine($"""<img src="{image.SourceUrl}" alt="{alt}" class="wp-image-{image.MediaId}" style="max-width: 100%; height: auto;" itemprop="url"/>""");
        sb.AppendLine("""<meta itemprop="width" content="1024"/>""");
        sb.AppendLine("""<meta itemprop="height" content="1024"/>""");
        sb.AppendLine($"""<figcaption itemprop="caption">{HttpUtility.HtmlEncode(fact.CarModel)} ({fact.Year})</figcaption>""");
        sb.AppendLine("</figure>");
    }

    private static void AppendBacklink(StringBuilder sb, BacklinkSuggestion backlink)
    {
        var escapedModel = HttpUtility.HtmlEncode(backlink.TargetCarModel);
        var escapedTitle = HttpUtility.HtmlEncode(backlink.TargetTitle);
        var escapedUrl = HttpUtility.HtmlEncode(backlink.TargetFactUrl);

        sb.AppendLine("""<div class="related-read" style="background: #f0f7ff; padding: 12px 16px; border-left: 3px solid #4a90d9; margin: 12px 0; font-size: 0.95em;">""");
        sb.AppendLine($"""<p style="margin: 0;">🔗 Speaking of which — the <a href="{escapedUrl}" title="{escapedTitle}">{escapedModel} ({backlink.TargetYear})</a> has a story worth knowing too.</p>""");
        sb.AppendLine("</div>");
    }

    private static void AppendConclusion(StringBuilder sb, List<CarFact> facts, string todayDate)
    {
        var firstYear = facts.First().Year;
        var lastYear = facts.Last().Year;

        sb.AppendLine("""<div class="car-facts-conclusion">""");
        sb.AppendLine("""<hr style="margin: 30px 0;"/>""");
        sb.AppendLine("<h3>🎯 Wrapping Up</h3>");
        sb.AppendLine($"<p>Pretty wild, right? These {facts.Count} moments from {HttpUtility.HtmlEncode(todayDate)} span " +
            $"from the {firstYear}s to the {lastYear}s — and each one left a serious mark on the auto world.</p>");
        sb.AppendLine("<p><strong>🔔 Want more?</strong> Come back tomorrow for another round of car history you probably didn't know about. " +
            "And hey, share this with your car-nerd friends!</p>");
        sb.AppendLine("</div>");
        sb.AppendLine();
    }

    private static void AppendRelatedPostsSection(StringBuilder sb, List<RelatedPostSuggestion> relatedPosts)
    {
        sb.AppendLine("""<div class="related-posts-section" style="margin-top: 30px;">""");
        sb.AppendLine("""<hr style="margin: 30px 0;"/>""");
        sb.AppendLine("<h3>🔍 You Might Also Find These Interesting</h3>");
        sb.AppendLine("""<div style="display: flex; flex-wrap: wrap; gap: 12px; margin-top: 15px;">""");

        foreach (var post in relatedPosts)
        {
            var escapedTitle = HttpUtility.HtmlEncode(post.PostTitle);
            var escapedUrl = HttpUtility.HtmlEncode(post.PostUrl);

            sb.AppendLine("""<div style="flex: 1 1 calc(50% - 12px); min-width: 200px; max-width: calc(50% - 6px); border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; background: #fafafa;">""");

            if (!string.IsNullOrEmpty(post.ImageUrl))
            {
                var escapedImg = HttpUtility.HtmlEncode(post.ImageUrl);
                sb.AppendLine($"""<a href="{escapedUrl}" style="display: block; overflow: hidden; height: 120px;">""");
                sb.AppendLine($"""<img src="{escapedImg}" alt="{escapedTitle}" style="width: 100%; height: 120px; object-fit: cover; display: block;"/>""");
                sb.AppendLine("</a>");
            }

            sb.AppendLine("""<div style="padding: 10px 12px;">""");
            sb.AppendLine($"""<a href="{escapedUrl}" style="text-decoration: none; color: #1a1a2e; font-weight: 600; font-size: 0.9em; line-height: 1.3; display: block;">{escapedTitle}</a>""");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // flex container
        sb.AppendLine("</div>"); // related-posts-section
    }

    private static void AppendFaqSection(StringBuilder sb, List<CarFact> facts, string todayDate)
    {
        var escapedDate = HttpUtility.HtmlEncode(todayDate);
        var firstYear = facts.First().Year;
        var lastYear = facts.Last().Year;

        sb.AppendLine("""<div class="faq-section" itemscope itemtype="https://schema.org/FAQPage" style="margin-top: 30px;">""");
        sb.AppendLine("<h3>❓ Frequently Asked Questions</h3>");
        sb.AppendLine("""<div itemscope itemprop="mainEntity" itemtype="https://schema.org/Question">""");
        sb.AppendLine($"""<h4 itemprop="name">What significant automotive events happened on {escapedDate}?</h4>""");
        sb.AppendLine("""<div itemscope itemprop="acceptedAnswer" itemtype="https://schema.org/Answer">""");
        sb.AppendLine($"""<p itemprop="text">On {escapedDate} throughout automotive history, {facts.Count} major events occurred, including groundbreaking launches, racing victories, and industry milestones spanning from {firstYear} to {lastYear}.</p>""");
        sb.AppendLine("</div></div>");
        sb.AppendLine("</div>");
    }
}
