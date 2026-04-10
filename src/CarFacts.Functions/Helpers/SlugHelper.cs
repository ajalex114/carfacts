using System.Text.RegularExpressions;
using CarFacts.Functions.Models;

namespace CarFacts.Functions.Helpers;

/// <summary>
/// Generates URL-friendly anchor IDs from car fact data.
/// </summary>
public static partial class SlugHelper
{
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
