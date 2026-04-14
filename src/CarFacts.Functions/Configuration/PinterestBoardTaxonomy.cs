namespace CarFacts.Functions.Configuration;

/// <summary>
/// Rule-based board taxonomy for Pinterest pin routing.
/// Maps keywords and car characteristics to a fixed set of board names
/// to prevent LLM-generated taxonomy sprawl.
/// </summary>
public static class PinterestBoardTaxonomy
{
    /// <summary>The default board for first-time pins.</summary>
    public const string DefaultBoard = "Car Facts";

    private static readonly Dictionary<string, string[]> BoardKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["American Muscle Cars"] = ["muscle", "mustang", "camaro", "corvette", "challenger", "charger", "firebird", "gto", "v8", "hemi", "pontiac", "dodge", "chevrolet", "ford"],
        ["Electric Vehicles"] = ["electric", "ev", "hybrid", "tesla", "leaf", "battery", "charging", "plug-in", "zero-emission"],
        ["Classic European Cars"] = ["ferrari", "porsche", "lamborghini", "mercedes", "bmw", "audi", "jaguar", "aston martin", "alfa romeo", "maserati", "bentley", "rolls-royce", "bugatti"],
        ["Japanese Automotive Icons"] = ["toyota", "honda", "nissan", "mazda", "subaru", "mitsubishi", "lexus", "acura", "infiniti", "datsun", "supra", "nsx", "skyline", "miata"],
        ["Vintage & Pre-War Cars"] = ["pre-war", "brass era", "model t", "ford model", "1920s", "1930s", "antique", "horseless carriage", "steam car"],
        ["Racing & Motorsport"] = ["racing", "formula", "nascar", "le mans", "grand prix", "indy", "rally", "motorsport", "track", "lap record", "speed record"],
        ["Trucks & SUVs"] = ["truck", "suv", "pickup", "off-road", "jeep", "land rover", "bronco", "4x4", "towing"],
        ["Luxury & Supercars"] = ["luxury", "supercar", "hypercar", "limousine", "v12", "exotic", "million dollar", "fastest", "top speed"],
        ["Automotive Innovation"] = ["invention", "patent", "first", "pioneer", "breakthrough", "technology", "safety", "airbag", "abs", "turbo", "fuel injection"],
        ["Car Culture & History"] = ["history", "culture", "road trip", "drive-in", "route 66", "automobile", "assembly line", "mass production"]
    };

    /// <summary>
    /// Selects the best board for a fact based on its keywords, car model, and title.
    /// Excludes boards the fact has already been posted to.
    /// Returns null if no suitable board is found (all boards exhausted).
    /// </summary>
    public static string? SelectBoard(
        string title,
        string carModel,
        List<string> keywords,
        int year,
        List<string> alreadyPostedBoards)
    {
        var searchText = $"{title} {carModel} {string.Join(" ", keywords)}".ToLowerInvariant();

        // Score each board by keyword matches
        var scored = BoardKeywords
            .Where(b => !alreadyPostedBoards.Contains(b.Key, StringComparer.OrdinalIgnoreCase))
            .Select(b => new
            {
                Board = b.Key,
                Score = b.Value.Count(kw => searchText.Contains(kw, StringComparison.OrdinalIgnoreCase))
            })
            .Where(b => b.Score > 0)
            .OrderByDescending(b => b.Score)
            .ToList();

        // Also consider era-based boards
        if (year > 0 && year < 1945
            && !alreadyPostedBoards.Contains("Vintage & Pre-War Cars", StringComparer.OrdinalIgnoreCase))
        {
            var vintage = scored.FirstOrDefault(s => s.Board == "Vintage & Pre-War Cars");
            if (vintage == null)
                scored.Add(new { Board = "Vintage & Pre-War Cars", Score = 1 });
        }

        return scored.FirstOrDefault()?.Board;
    }

    /// <summary>All known board names in the taxonomy.</summary>
    public static IReadOnlyList<string> AllBoards => [DefaultBoard, .. BoardKeywords.Keys];
}
