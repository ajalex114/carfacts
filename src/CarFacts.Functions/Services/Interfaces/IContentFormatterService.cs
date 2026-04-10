using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IContentFormatterService
{
    string FormatPostHtml(CarFactsResponse response, List<UploadedMedia> media, string todayDate);
    string FormatPostHtml(RawCarFactsContent content, SeoMetadata seo, List<UploadedMedia> media, string todayDate, List<BacklinkSuggestion>? backlinks = null);
    string FormatPostHtmlWithBase64Images(CarFactsResponse response, List<GeneratedImage> images, string todayDate);
}
