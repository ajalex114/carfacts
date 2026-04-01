using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

/// <summary>
/// Decorator that caches generated images locally.
/// First run: calls the real API and saves images to disk.
/// Subsequent runs: loads images from disk, skipping the API entirely.
/// </summary>
public sealed class CachedImageGenerationService : IImageGenerationService
{
    private static readonly string CacheDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "cache", "images");

    private readonly IImageGenerationService _inner;
    private readonly ILogger<CachedImageGenerationService> _logger;

    public CachedImageGenerationService(
        IImageGenerationService inner,
        ILogger<CachedImageGenerationService> logger)
    {
        _inner = inner;
        _logger = logger;
        Directory.CreateDirectory(CacheDir);
    }

    public async Task<List<GeneratedImage>> GenerateImagesAsync(
        List<CarFact> facts,
        CancellationToken cancellationToken = default)
    {
        var cached = TryLoadFromCache(facts);
        if (cached is not null)
            return cached;

        _logger.LogInformation("No cached images found — calling Stability AI API");
        var images = await _inner.GenerateImagesAsync(facts, cancellationToken);

        SaveToCache(images);
        return images;
    }

    private List<GeneratedImage>? TryLoadFromCache(List<CarFact> facts)
    {
        var files = Directory.GetFiles(CacheDir, "*.png");
        if (files.Length < facts.Count)
            return null;

        _logger.LogWarning("Using {Count} cached images from {Dir} — delete folder to force regeneration", files.Length, CacheDir);

        var sorted = files.OrderBy(f => f).ToList();
        return sorted
            .Take(facts.Count)
            .Select((file, index) => new GeneratedImage
            {
                FactIndex = index,
                ImageData = File.ReadAllBytes(file),
                FileName = Path.GetFileName(file)
            })
            .ToList();
    }

    private void SaveToCache(List<GeneratedImage> images)
    {
        foreach (var image in images)
        {
            var path = Path.Combine(CacheDir, image.FileName);
            File.WriteAllBytes(path, image.ImageData);
            _logger.LogInformation("Cached image: {Path}", path);
        }
    }
}
