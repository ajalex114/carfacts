using CarFacts.Functions.Models;

namespace CarFacts.Functions.Tests.Helpers;

public static class TestDataBuilder
{
    public static CarFactsResponse CreateValidResponse(int factCount = 5)
    {
        return new CarFactsResponse
        {
            MainTitle = "5 Shocking Car Moments from March 21 That Rewrote History",
            MetaDescription = "Discover 5 incredible automotive milestones that happened on March 21 throughout history, from iconic launches to racing legends.",
            Keywords = ["automotive history", "car facts", "March 21", "classic cars", "car milestones"],
            GeoSummary = "On March 21 throughout automotive history, five major events shaped the car industry. These include iconic vehicle launches, racing victories, and engineering breakthroughs spanning multiple decades.",
            Facts = Enumerable.Range(0, factCount).Select(i => CreateFact(i)).ToList()
        };
    }

    public static CarFact CreateFact(int index = 0)
    {
        var decades = new[] { 1935, 1957, 1972, 1988, 2015 };
        var models = new[] { "Ford Model 48", "Chevrolet Bel Air", "BMW 3.0 CSL", "Ferrari F40", "Tesla Model X" };
        var titles = new[]
        {
            "The Secret Ford Nobody Talks About",
            "When Chevrolet Changed Everything Forever",
            "BMW's Hidden Racing Weapon Revealed",
            "Ferrari's Most Shocking Supercar Debut",
            "Tesla's Gamble That Paid Off Big"
        };

        var idx = index % decades.Length;
        return new CarFact
        {
            Year = decades[idx],
            CatchyTitle = titles[idx],
            Fact = $"On this day in {decades[idx]}, the {models[idx]} was revealed to the world, marking a significant milestone in automotive history. This groundbreaking vehicle introduced several innovations that would shape the industry for decades to come.",
            CarModel = models[idx],
            ImagePrompt = $"A photorealistic image of {models[idx]}, gleaming under studio lights, professional automotive photography, 8k"
        };
    }

    public static List<GeneratedImage> CreateGeneratedImages(int count = 5)
    {
        return Enumerable.Range(0, count).Select(i => new GeneratedImage
        {
            FactIndex = i,
            ImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG header stub
            FileName = $"car-fact-{1935 + i * 20}-{i + 1}.png"
        }).ToList();
    }

    public static List<UploadedMedia> CreateUploadedMedia(int count = 5)
    {
        return Enumerable.Range(0, count).Select(i => new UploadedMedia
        {
            FactIndex = i,
            MediaId = 100 + i,
            SourceUrl = $"https://example.com/wp-content/uploads/car-fact-{i + 1}.png"
        }).ToList();
    }

    public static WordPressPostResult CreatePostResult()
    {
        return new WordPressPostResult
        {
            PostId = 42,
            PostUrl = "https://example.com/shocking-car-moments-march-21/",
            Title = "5 Shocking Car Moments from March 21 That Rewrote History",
            PublishedAt = new DateTime(2026, 3, 21, 6, 0, 0, DateTimeKind.Utc)
        };
    }

    public static string CreateOpenAIResponseJson(CarFactsResponse response)
    {
        var contentJson = System.Text.Json.JsonSerializer.Serialize(response);
        var escapedContent = System.Text.Json.JsonSerializer.Serialize(contentJson);
        return $$"""
        {
          "choices": [
            {
              "message": {
                "content": {{escapedContent}}
              }
            }
          ]
        }
        """;
    }

    public static string CreateStabilityAIResponseJson()
    {
        var fakeBase64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        return $$"""
        {
          "artifacts": [
            {
              "base64": "{{fakeBase64}}"
            }
          ]
        }
        """;
    }

    public static string CreateWordPressMediaResponseJson(int mediaId, int factIndex)
    {
        return $$"""
        {
          "media": [
            {
              "ID": {{mediaId}},
              "URL": "https://example.com/wp-content/uploads/car-fact-{{factIndex + 1}}.png"
            }
          ]
        }
        """;
    }

    public static string CreateWordPressPostResponseJson()
    {
        return """
        {
          "ID": 42,
          "URL": "https://example.com/shocking-car-moments-march-21/",
          "title": "5 Shocking Car Moments from March 21 That Rewrote History",
          "date": "2026-03-21T06:00:00"
        }
        """;
    }
}
