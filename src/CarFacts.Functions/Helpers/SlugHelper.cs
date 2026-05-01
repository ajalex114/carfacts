using System.Text.RegularExpressions;
using CarFacts.Functions.Models;

namespace CarFacts.Functions.Helpers;

/// <summary>
/// Generates URL-friendly anchor IDs from car fact data.
/// </summary>
public static partial class SlugHelper
{
/// <summary>
    /// Generates a URL-friendly slug from a post title.
    /// e.g., "Five Wild Moments in Car History" → "five-wild-moments-in-car-history"
    /// </summary>
    public static string GeneratePostSlug(string title)
    {
        var lower = title.ToLowerInvariant();
        var slug = NonAlphanumericRegex().Replace(lower, "-").Trim('-');
        slug = MultipleHyphensRegex().Replace(slug, "-");
        // Truncate to 80 chars to keep URLs clean
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('-');
        return slug;
    }

    /// <summary>
    /// Generates a meaningful anchor ID from a CarFact's model name and year.
    /// e.g., "BMW 3.0 CSL" + 1972 → "bmw-3-0-csl-1972"
    /// </summary>
    public static string GenerateAnchorId(CarFact fact)
    {
        var model = fact.CarModel.ToLowerInvariant();
        var slug = NonAlphanumericRegex().Replace(model, "-").Trim('-');
        slug = MultipleHyphensRegex().Replace(slug, "-");
        return $"{slug}-{fact.Year}";
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
