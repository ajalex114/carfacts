using System.Text;
using System.Web;
using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services;

/// <summary>
/// Formats article HTML for Medium — cleaner markup without WordPress-specific elements.
/// </summary>
public static class MediumContentFormatter
{
    public static string FormatForMedium(
        RawCarFactsContent content,
        SeoMetadata seo,
        string postUrl,
        string todayDate,
        List<UploadedMedia> media)
    {
        var sb = new StringBuilder(4096);

        // Attribution note at the top
        sb.AppendLine($"""<p><em>This article was originally published at <a href="{HttpUtility.HtmlAttributeEncode(postUrl)}">{HttpUtility.HtmlEncode(postUrl)}</a></em></p>""");
        sb.AppendLine("<hr/>");
        sb.AppendLine();

        // Intro
        sb.AppendLine($"<p>On this day in automotive history — {HttpUtility.HtmlEncode(todayDate)} — " +
            "here are five wild moments that shaped the car world as we know it.</p>");
        sb.AppendLine();

        // Each fact section — clean HTML only
        foreach (var (fact, idx) in content.Facts.Select((f, i) => (f, i)))
        {
            var escapedTitle = HttpUtility.HtmlEncode(fact.CatchyTitle);
            var escapedModel = HttpUtility.HtmlEncode(fact.CarModel);

            sb.AppendLine($"<h2>{escapedTitle}</h2>");
            sb.AppendLine($"<p><em>Year: {fact.Year} | Vehicle: {escapedModel}</em></p>");

            // Include image if available
            var image = media.FirstOrDefault(m => m.FactIndex == idx);
            if (image is not null && !string.IsNullOrEmpty(image.SourceUrl))
            {
                sb.AppendLine($"""<figure><img src="{HttpUtility.HtmlAttributeEncode(image.SourceUrl)}" alt="{HttpUtility.HtmlAttributeEncode(fact.CarModel)} from {fact.Year}"/></figure>""");
            }

            sb.AppendLine($"<p>{HttpUtility.HtmlEncode(fact.Fact)}</p>");
            sb.AppendLine();
        }

        // Simple conclusion
        sb.AppendLine("<h2>Wrapping Up</h2>");
        sb.AppendLine("<p>These stories remind us that the automotive world has always been full of surprises. " +
            "Every era brought its own innovations, scandals, and breakthroughs that shaped how we drive today.</p>");

        return sb.ToString();
    }
}
