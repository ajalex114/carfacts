using CarFacts.VideoPoC.Models;
using System.Text.RegularExpressions;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// Splits word timings into sentence-level segments and generates
/// a Pexels search query for each segment using simple keyword extraction.
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

    // Known car brands: key = word to detect (case-insensitive), value = Pexels-friendly brand name
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
    private static string? DetectBrand(string factText)
    {
        // Check multi-word brand keys first (longest match wins)
        var multiWord = BrandMap.Keys
            .Where(k => k.Contains(' '))
            .OrderByDescending(k => k.Length);

        foreach (var key in multiWord)
            if (factText.Contains(key, StringComparison.OrdinalIgnoreCase))
                return BrandMap[key];

        // Then single-word keys — match on whole word boundary
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

    /// <summary>
    /// Splits words into segments at sentence boundaries (period/question/exclamation
    /// or pauses ≥ 0.4s), then force-splits any segment longer than MaxClipDuration.
    /// The last segment is extended to <paramref name="totalDuration"/> to cover the hook.
    /// </summary>
    public static List<VideoSegment> Plan(
        List<WordTiming> words,
        double totalDuration,
        string factContext = "")
    {
        // ── Step 1: split at sentence boundaries ────────────────────────────
        var groups = new List<List<WordTiming>>();
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
                // Split roughly in the middle
                int mid = g.Count / 2;
                finalGroups.Add(g[..mid]);
                finalGroups.Add(g[mid..]);
            }
            else
            {
                finalGroups.Add(g);
            }
        }

        // ── Step 3: detect brand for brand-aware query injection ─────────────
        string? detectedBrand = string.IsNullOrWhiteSpace(factContext)
            ? null
            : DetectBrand(factContext);

        if (detectedBrand != null)
            Console.WriteLine($"🏷️   Brand detected: {detectedBrand} — clips will be brand-specific");

        // ── Step 4: build VideoSegment list ──────────────────────────────────
        var segments = new List<VideoSegment>();
        for (int i = 0; i < finalGroups.Count; i++)
        {
            var g     = finalGroups[i];
            var start = g[0].StartSeconds;
            var end   = i == finalGroups.Count - 1
                ? totalDuration
                : finalGroups[i + 1][0].StartSeconds;

            segments.Add(new VideoSegment(
                SearchQuery:  BuildQuery(g, detectedBrand),
                StartSeconds: start,
                EndSeconds:   end));
        }

        return segments;
    }

    private static string BuildQuery(List<WordTiming> group, string? brand = null)
    {
        var rawWords = group
            .Select(w => Regex.Replace(w.Word, @"[^a-zA-Z0-9]", ""))
            .Where(w => w.Length > 2)
            .ToList();

        // Check domain keyword map first (highest specificity)
        foreach (var w in rawWords)
            if (KeywordMap.TryGetValue(w, out var mapped))
                // Prepend brand to domain-mapped query (e.g. "Ford car factory assembly line")
                return brand != null ? $"{brand} {mapped}" : mapped;

        // Replace years
        var words = rawWords
            .Where(w => !StopWords.Contains(w))
            .Select(w => YearMap.TryGetValue(w, out var y) ? y : w)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        // If brand is known, it IS the car-related anchor — prepend and don't add generic "car"
        if (brand != null)
        {
            // Remove the brand word itself if it ended up in the word list (redundant)
            words = words
                .Where(w => !w.Equals(brand.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                .ToList();
            words.Insert(0, brand);
        }
        else if (!words.Any(w => w.Contains("car", StringComparison.OrdinalIgnoreCase)
                             || w.Contains("auto", StringComparison.OrdinalIgnoreCase)
                             || w.Contains("vehicle", StringComparison.OrdinalIgnoreCase)
                             || w.Contains("driv", StringComparison.OrdinalIgnoreCase)))
        {
            words.Add("car");
        }

        return string.Join(" ", words.Take(4));
    }
}

