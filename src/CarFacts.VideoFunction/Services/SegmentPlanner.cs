using CarFacts.VideoFunction.Models;
using System.Text.RegularExpressions;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Splits word timings into sentence-level segments and assigns each one the shared
/// image search query produced by <see cref="ImageQueryExtractorService"/>.
/// </summary>
public static class SegmentPlanner
{
    private const double MaxClipDuration = 3.5; // force-split segments longer than this

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","is","are","was","were","be","been","being",
        "have","has","had","do","does","did","will","would","could","should","may",
        "might","shall","can","to","of","in","for","on","with","at","by","from","up",
        "about","into","through","as","it","its","out","not","then","that","this",
        "these","those","there","they","them","their","which","who","what","when",
        "where","how","why","very","just","back","than","so","yet","only","also",
        "over","such","if","per","then","than","even","well","back","been","also",
        "never","every","because","first","last","both","new","one","two","three"
    };

    // Known car brands: key = word to detect (case-insensitive), value = Bing-friendly brand name
    private static readonly Dictionary<string, string> BrandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Ford",       "Ford"       }, { "Model T",    "Ford Model T" },
        { "Toyota",     "Toyota"     }, { "Honda",      "Honda"        },
        { "BMW",        "BMW"        }, { "Mercedes",   "Mercedes"     },
        { "Benz",       "Mercedes"   }, { "Volkswagen", "Volkswagen"   },
        { "VW",         "Volkswagen" }, { "Audi",       "Audi"         },
        { "Chevrolet",  "Chevrolet"  }, { "Chevy",      "Chevrolet"    },
        { "Porsche",    "Porsche"    }, { "Ferrari",    "Ferrari"       },
        { "Lamborghini","Lamborghini"}, { "Bugatti",    "Bugatti"       },
        { "Tesla",      "Tesla"      }, { "Dodge",      "Dodge"         },
        { "Jeep",       "Jeep"       }, { "Rolls",      "Rolls Royce"   },
        { "Royce",      "Rolls Royce"}, { "Bentley",    "Bentley"       },
        { "Maserati",   "Maserati"   }, { "Alfa",       "Alfa Romeo"    },
        { "Romeo",      "Alfa Romeo" }, { "Jaguar",     "Jaguar"        },
        { "Aston",      "Aston Martin"},{ "Martin",     "Aston Martin"  },
        { "McLaren",    "McLaren"    }, { "Pagani",     "Pagani"         },
        { "Lotus",      "Lotus"      }, { "Subaru",     "Subaru"         },
        { "Mitsubishi", "Mitsubishi" }, { "Nissan",     "Nissan"         },
        { "Mazda",      "Mazda"      }, { "Kia",        "Kia"            },
        { "Hyundai",    "Hyundai"    }, { "Volvo",      "Volvo"          },
        { "Peugeot",    "Peugeot"    }, { "Renault",    "Renault"        },
        { "Fiat",       "Fiat"       }, { "Chrysler",   "Chrysler"       },
        { "Cadillac",   "Cadillac"   }, { "Lincoln",    "Lincoln car"    },
        { "Buick",      "Buick"      }, { "GMC",        "GMC truck"      },
        { "Pontiac",    "Pontiac"    }, { "Oldsmobile", "Oldsmobile"     },
        { "Studebaker", "Studebaker" }, { "Packard",    "Packard car"    },
        { "Citroën",    "Citroen"    }, { "Citroen",    "Citroen"        },
        { "Seat",       "Seat car"   }, { "Skoda",      "Skoda"          },
        { "Opel",       "Opel"       }, { "Vauxhall",   "Vauxhall"       },
        { "Hummer",     "Hummer"     }, { "Land Rover", "Land Rover"     },
        { "Rover",      "Land Rover" }, { "Mini",       "Mini car"       },
        { "Lexus",      "Lexus"      }, { "Acura",      "Acura"          },
        { "Infiniti",   "Infiniti"   }, { "Genesis",    "Genesis car"    },
    };

    /// <summary>
    /// Detects the first car brand mentioned in the fact text.
    /// Multi-word brands (e.g. "Model T", "Land Rover") are checked before single words.
    /// Returns null if no known brand is found.
    /// </summary>
    internal static string? DetectBrand(string factText)
    {
        var multiWord = BrandMap.Keys
            .Where(k => k.Contains(' '))
            .OrderByDescending(k => k.Length);

        foreach (var key in multiWord)
            if (factText.Contains(key, StringComparison.OrdinalIgnoreCase))
                return BrandMap[key];

        foreach (var key in BrandMap.Keys.Where(k => !k.Contains(' ')))
            if (Regex.IsMatch(factText, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase))
                return BrandMap[key];

        return null;
    }

    // Year-like numbers → useful visual search term
    private static readonly Dictionary<string, string> YearMap = new()
    {
        { "1900", "vintage car" }, { "1901", "vintage car" },
        { "1902", "vintage car" }, { "1903", "vintage car" },
        { "1904", "vintage car" }, { "1905", "vintage car" },
        { "1908", "vintage car" }, { "1909", "vintage car" },
        { "1910", "antique car" }, { "1920", "antique car" },
        { "1930", "classic car" }, { "1940", "classic car" },
        { "1950", "retro car"   }, { "1960", "retro car"   },
        { "1970", "muscle car"  }, { "1980", "vintage car" },
    };

    // Domain-specific keyword → better visual search term
    private static readonly Dictionary<string, string> KeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "assembly",   "car factory assembly line" },
        { "production", "car factory production"    },
        { "factory",    "car factory"               },
        { "paint",      "car paint spray"           },
        { "painted",    "car paint spray"           },
        { "black",      "black car"                 },
        { "speed",      "fast car driving"          },
        { "speeding",   "car speeding highway"      },
        { "ticket",     "police car traffic stop"   },
        { "limit",      "speed limit sign road"     },
        { "driving",    "car driving road"          },
        { "drive",      "car driving road"          },
        { "engine",     "car engine close up"       },
        { "race",       "racing car track"          },
        { "crash",      "car crash slow motion"     },
        { "electric",   "electric car charging"     },
        { "fuel",       "car fuel station"          },
        { "highway",    "cars highway driving"      },
        { "road",       "car road driving"          },
    };

    // Known car models: checked only after a brand is detected
    private static readonly HashSet<string> ModelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ford
        "Mustang", "F-150", "F-250", "Bronco", "Explorer", "Ranger", "Maverick",
        "GT40", "GT500", "Shelby", "Thunderbird", "Pinto", "Fairlane", "Galaxie",
        // Toyota
        "Camry", "Corolla", "Supra", "Prius", "Celica", "Tacoma", "Tundra",
        "Sequoia", "Highlander", "Land Cruiser", "4Runner", "Yaris", "Avalon",
        // Honda
        "Civic", "Accord", "NSX", "S2000", "Prelude", "Integra",
        // Chevrolet / GM
        "Corvette", "Camaro", "Silverado", "Tahoe", "Suburban", "Impala",
        "Blazer", "Bel Air", "El Camino", "Malibu",
        // Dodge / Chrysler
        "Challenger", "Charger", "Viper", "Hellcat", "Durango",
        // Porsche
        "Boxster", "Cayenne", "Panamera", "Macan", "Carrera", "Targa",
        // Ferrari
        "LaFerrari", "Testarossa", "Enzo", "Stradale",
        // Lamborghini
        "Aventador", "Huracan", "Urus", "Countach", "Diablo", "Gallardo",
        // Tesla
        "Cybertruck", "Roadster",
        // VW
        "Beetle", "Passat",
        // BMW
        "Isetta",
        // Other
        "Miura", "Zonda", "Chiron", "Veyron", "Speedster", "Carrera GT",
    };

    internal static string? DetectModel(string factText)
    {
        // Multi-word models first
        var multiWord = ModelNames.Where(m => m.Contains(' ')).OrderByDescending(m => m.Length);
        foreach (var m in multiWord)
            if (factText.Contains(m, StringComparison.OrdinalIgnoreCase))
                return m;

        foreach (var m in ModelNames.Where(m => !m.Contains(' ')))
            if (Regex.IsMatch(factText, $@"\b{Regex.Escape(m)}\b", RegexOptions.IgnoreCase))
                return m;

        return null;
    }


    /// <summary>Fallback suffix for rule-based query building (only used when no LLM query available).</summary>
    private static readonly Dictionary<ShotType, string> ShotTypeSuffix = new()
    {
        { ShotType.ExteriorRolling, "automobile"    },
        { ShotType.InteriorPOV,     "car interior"  },
        { ShotType.DroneShot,       "car photo"     },
        { ShotType.CloseUp,         "car close up"  },
    };

    /// <summary>
    /// Splits words into segments at sentence boundaries (period/question/exclamation
    /// or pauses ≥ 0.4s), then force-splits any segment longer than MaxClipDuration.
    /// Each segment is assigned a shuffled shot type for visual variety.
    /// </summary>
    public static List<VideoSegment> Plan(
        List<WordTiming> words,
        double totalDuration,
        string factContext = "",
        string? imageSearchQuery = null)
    {
        // ── Step 1: split at sentence boundaries ────────────────────────────
        var groups  = new List<List<WordTiming>>();
        var current = new List<WordTiming>();

        for (int i = 0; i < words.Count; i++)
        {
            current.Add(words[i]);

            bool sentenceEnd = Regex.IsMatch(words[i].Word, @"[.?!,]$");
            bool longPause   = i < words.Count - 1
                && (words[i + 1].StartSeconds - words[i].EndSeconds) >= 0.4;

            if ((sentenceEnd || longPause) && current.Count >= 2)
            {
                groups.Add(current);
                current = [];
            }
        }
        if (current.Count > 0) groups.Add(current);

        // ── Step 2: force-split any group longer than MaxClipDuration ────────
        var finalGroups = new List<List<WordTiming>>();
        foreach (var g in groups)
        {
            double dur = g[^1].EndSeconds - g[0].StartSeconds;
            if (dur > MaxClipDuration && g.Count >= 4)
            {
                int mid = g.Count / 2;
                finalGroups.Add(g[..mid]);
                finalGroups.Add(g[mid..]);
            }
            else
            {
                finalGroups.Add(g);
            }
        }

        // ── Step 3: detect brand + model ────────────────────────────────────
        string? detectedBrand = null;
        string? detectedModel = null;

        if (imageSearchQuery != null)
        {
            Console.WriteLine($"🔍  Using LLM image_search_query: \"{imageSearchQuery}\"");
        }
        else if (!string.IsNullOrWhiteSpace(factContext))
        {
            detectedBrand = DetectBrand(factContext);
            detectedModel = detectedBrand != null ? DetectModel(factContext) : null;
            if (detectedBrand != null)
                Console.WriteLine($"🏷️   Brand detected: {detectedBrand}{(detectedModel != null ? $" {detectedModel}" : "")} — clips will be brand-specific");
        }

        // ── Step 4: generate shuffled shot type sequence ─────────────────────
        var shotSequence = ShuffleShotTypes(finalGroups.Count);

        // ── Step 5: build VideoSegment list ──────────────────────────────────
        var segments = new List<VideoSegment>();
        for (int i = 0; i < finalGroups.Count; i++)
        {
            var g       = finalGroups[i];
            var start   = g[0].StartSeconds;
            var end     = i == finalGroups.Count - 1
                ? totalDuration
                : finalGroups[i + 1][0].StartSeconds;
            var shot    = shotSequence[i];

            // Use LLM-provided query if available; fall back to text-derived query
            var query = imageSearchQuery ?? BuildQuery(detectedBrand, detectedModel, shot);

            Console.WriteLine($"  Segment {i}: [{shot}] \"{query}\"");

            var fallback = detectedBrand != null
                ? $"{detectedBrand}{(detectedModel != null ? $" {detectedModel}" : "")} automobile"
                : "automobile car photo";

            var brandOnlyFallback = detectedBrand != null && detectedModel != null
                ? $"{detectedBrand} automobile"
                : null;

            segments.Add(new VideoSegment(
                SearchQuery:  query,
                StartSeconds: start,
                EndSeconds:   end,
                ShotType:     shot)
                with { FallbackQuery = fallback, BrandOnlyFallback = brandOnlyFallback });
        }

        return segments;
    }

    /// <summary>
    /// Generates a shuffled sequence of shot types, distributed evenly across N segments.
    /// Uses Fisher-Yates over the 4 types, cycling as needed so no consecutive run of
    /// the same shot type appears if avoidable.
    /// </summary>
    private static List<ShotType> ShuffleShotTypes(int count)
    {
        var all   = Enum.GetValues<ShotType>();
        var rng   = Random.Shared;
        var pool  = new List<ShotType>();

        // Fill pool with enough full cycles
        while (pool.Count < count)
            pool.AddRange(all);

        // Fisher-Yates shuffle on what we need
        for (int i = 0; i < count; i++)
        {
            int j     = rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(count).ToList();
    }

    /// <summary>
    /// Builds a Bing image search query — only used when no LLM-generated query is available.
    /// Uses brand + model when detected (e.g. "Ford Mustang automobile") for specific results.
    /// </summary>
    private static string BuildQuery(string? brand, string? model, ShotType shot)
    {
        var carBase = model != null  ? $"{brand} {model}" :
                      brand != null  ? $"{brand} car"     : "car";
        return $"{carBase} {ShotTypeSuffix[shot]}";
    }
}
