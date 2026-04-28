using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Uses Azure Computer Vision to check YouTube thumbnails for:
/// 1. Watermarks / text overlays (burned-in channel branding, subtitles)
/// 2. Car presence — every clip must show a car
/// Both checks run in a single API call on the ~30 KB hqdefault thumbnail.
/// </summary>
public class ComputerVisionService(string endpoint, string apiKey)
{
    // Corner zone thresholds — typical watermark positions
    private const double CornerWidthFraction  = 0.30;
    private const double CornerHeightFraction = 0.20;
    private const int    WatermarkCharThreshold = 8;
    // If entire thumbnail has more than this many text chars it's a title card, not real footage
    private const int    TextHeavyCharThreshold = 40;

    // Tags that indicate a car is present (confidence ≥ CarTagMinConfidence required)
    private static readonly HashSet<string> CarTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "car", "vehicle", "automobile", "automotive", "wheel", "tire",
        "sports car", "race car", "truck", "suv", "convertible", "sedan",
        "coupe", "supercar", "hypercar", "muscle car", "vintage car", "classic car",
        "steering wheel", "dashboard", "windshield", "hood", "bumper",
    };
    private const float CarTagMinConfidence = 0.60f;

    public record ThumbnailAnalysis(bool HasWatermark, bool HasCar, bool IsTextHeavy = false);

    /// <summary>
    /// Analyzes a YouTube video thumbnail.
    /// Returns HasWatermark=true if significant corner text detected.
    /// Returns HasCar=true if any car-related tag is found with sufficient confidence.
    /// Returns IsTextHeavy=true if the full image contains too much text — indicates a static
    /// title-card or fake-footage upload (spam CC channels), not real video footage.
    /// On any failure, returns HasWatermark=false + HasCar=true + IsTextHeavy=false (optimistic).
    /// </summary>
    public async Task<ThumbnailAnalysis> AnalyzeThumbnailAsync(string videoId)
    {
        try
        {
            var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";

            var client = new ImageAnalysisClient(
                new Uri(endpoint),
                new AzureKeyCredential(apiKey));

            var result = await client.AnalyzeAsync(
                new Uri(thumbnailUrl),
                VisualFeatures.Read | VisualFeatures.Tags);

            var hasWatermark = CheckWatermark(result?.Value);
            var hasCar       = CheckCarPresence(result?.Value);
            var isTextHeavy  = CheckTextHeavy(result?.Value);

            return new ThumbnailAnalysis(hasWatermark, hasCar, isTextHeavy);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  CV check failed for {videoId}: {ex.Message}");
            return new ThumbnailAnalysis(HasWatermark: false, HasCar: true); // optimistic fallback
        }
    }

    private static bool CheckWatermark(ImageAnalysisResult? result)
    {
        if (result?.Read?.Blocks is null) return false;

        const double imageWidth  = 480.0;
        const double imageHeight = 360.0;
        int cornerChars = 0;

        foreach (var block in result.Read.Blocks)
        foreach (var line in block.Lines)
        {
            var bounds = line.BoundingPolygon;
            if (bounds.Count < 4) continue;

            double cx = bounds.Average(p => p.X);
            double cy = bounds.Average(p => p.Y);

            bool inCorner =
                cx < imageWidth  * CornerWidthFraction  ||
                cx > imageWidth  * (1 - CornerWidthFraction)  ||
                cy < imageHeight * CornerHeightFraction ||
                cy > imageHeight * (1 - CornerHeightFraction);

            if (inCorner)
                cornerChars += line.Text.Length;
        }

        return cornerChars >= WatermarkCharThreshold;
    }

    private static bool CheckCarPresence(ImageAnalysisResult? result)
    {
        if (result?.Tags?.Values is null) return false;

        return result.Tags.Values.Any(tag =>
            CarTags.Contains(tag.Name) && tag.Confidence >= CarTagMinConfidence);
    }

    /// <summary>
    /// Counts all text characters across the entire thumbnail.
    /// A high count indicates a static title card or fake-footage upload — reject these.
    /// Genuine car footage thumbnails have a real scene with little or no text overlay.
    /// </summary>
    private static bool CheckTextHeavy(ImageAnalysisResult? result)
    {
        if (result?.Read?.Blocks is null) return false;

        int totalChars = result.Read.Blocks
            .SelectMany(b => b.Lines)
            .Sum(l => l.Text.Length);

        return totalChars >= TextHeavyCharThreshold;
    }
}
