using System.Net;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Creates a Google Web Story from the daily car facts and publishes it.
/// Each story has a cover page, one page per fact, and a CTA page linking to the blog post.
/// </summary>
public sealed class CreateWebStoryActivity
{
    private readonly IWordPressService _wordPressService;
    private readonly ISecretProvider _secretProvider;
    private readonly WebStoriesSettings _settings;
    private readonly ILogger<CreateWebStoryActivity> _logger;

    // Dark automotive color palette for page backgrounds
    private static readonly string[] PageColors =
    [
        "#1a1a2e", // deep navy
        "#16213e", // dark blue
        "#0f3460", // royal blue
        "#533483", // deep purple
        "#1b1b2f", // midnight
        "#2c003e", // dark violet
        "#0a1628"  // navy black
    ];

    private const string AccentColor = "#e94560";
    private const string TextColor = "#ffffff";
    private const string SubtextColor = "#b0b0b0";
    private const string BrandingText = "CarFactsDaily.com";

    public CreateWebStoryActivity(
        IWordPressService wordPressService,
        ISecretProvider secretProvider,
        IOptions<WebStoriesSettings> settings,
        ILogger<CreateWebStoryActivity> logger)
    {
        _wordPressService = wordPressService;
        _secretProvider = secretProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    [Function(nameof(CreateWebStoryActivity))]
    public async Task<WordPressPostResult> Run(
        [ActivityTrigger] CreateWebStoryInput input)
    {
        _logger.LogInformation("Creating Web Story for: {Title} with {FactCount} facts",
            input.MainTitle, input.Facts.Count);

        var adSenseClientId = await GetOptionalSecretAsync(SecretNames.AdSenseClientId);
        var adSenseSlotId = await GetOptionalSecretAsync(SecretNames.AdSenseSlotId);

        var storyContent = BuildStoryMarkup(input, adSenseClientId, adSenseSlotId);
        var excerpt = TruncateText(input.Excerpt, 200);

        var result = await _wordPressService.CreateWebStoryAsync(
            input.MainTitle,
            storyContent,
            excerpt);

        _logger.LogInformation("Web Story published: {StoryUrl}", result.PostUrl);
        return result;
    }

    private async Task<string> GetOptionalSecretAsync(string secretName)
    {
        try
        {
            return await _secretProvider.GetSecretAsync(secretName);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildStoryMarkup(CreateWebStoryInput input, string adSenseClientId, string adSenseSlotId)
    {
        var pages = new List<string>();

        // Build image lookup by fact index
        var imagesByFact = input.Media.ToDictionary(m => m.FactIndex, m => m.SourceUrl);

        // Cover page — use first image if available
        var coverImage = imagesByFact.GetValueOrDefault(0, string.Empty);
        pages.Add(BuildCoverPage(input.MainTitle, coverImage));

        // One page per fact
        for (var i = 0; i < input.Facts.Count; i++)
        {
            var fact = input.Facts[i];
            var bgColor = PageColors[(i + 1) % PageColors.Length];
            var imageUrl = imagesByFact.GetValueOrDefault(i, string.Empty);
            pages.Add(BuildFactPage(fact, i + 1, bgColor, imageUrl));
        }

        // CTA page
        pages.Add(BuildCtaPage(input.PostUrl));

        var publisherLogo = !string.IsNullOrEmpty(_settings.PublisherLogoUrl)
            ? _settings.PublisherLogoUrl
            : "https://carfactsdaily.com/wp-content/uploads/2026/04/cropped-Copilot_20260414_172128.png";

        var posterImage = !string.IsNullOrEmpty(input.FeaturedImageUrl)
            ? input.FeaturedImageUrl
            : publisherLogo;

        var autoAdsBlock = BuildAutoAdsBlock(adSenseClientId, adSenseSlotId);

        return $@"<amp-story standalone
  title=""{Escape(input.MainTitle)}""
  publisher=""{Escape(_settings.PublisherName)}""
  publisher-logo-src=""{Escape(publisherLogo)}""
  poster-portrait-src=""{Escape(posterImage)}"">
{autoAdsBlock}{string.Join("\n", pages)}
</amp-story>";
    }

    private static string BuildCoverPage(string title, string imageUrl)
    {
        var backgroundLayer = !string.IsNullOrEmpty(imageUrl)
            ? $@"    <amp-story-grid-layer template=""fill"">
      <amp-img src=""{Escape(imageUrl)}"" width=""720"" height=""1280"" layout=""fill"" object-fit=""cover""></amp-img>
    </amp-story-grid-layer>
    <amp-story-grid-layer template=""fill"">
      <div style=""background: linear-gradient(0deg, rgba(0,0,0,0.8) 0%, rgba(0,0,0,0.3) 50%, rgba(0,0,0,0.6) 100%); width:100%; height:100%;""></div>
    </amp-story-grid-layer>"
            : $@"    <amp-story-grid-layer template=""fill"">
      <div style=""background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%); width:100%; height:100%;""></div>
    </amp-story-grid-layer>";

        return $@"  <amp-story-page id=""cover"">
{backgroundLayer}
    <amp-story-grid-layer template=""thirds"">
      <div grid-area=""upper-third""></div>
      <div grid-area=""middle-third"" style=""display:flex; align-items:center; justify-content:center; padding:20px;"">
        <h1 style=""color:{TextColor}; font-size:28px; text-align:center; line-height:1.3; font-family:sans-serif; text-shadow: 0 2px 8px rgba(0,0,0,0.7);"">{Escape(title)}</h1>
      </div>
      <div grid-area=""lower-third"" style=""display:flex; align-items:flex-end; justify-content:center; padding:20px;"">
        <p style=""color:{AccentColor}; font-size:14px; font-family:sans-serif; text-shadow: 0 1px 4px rgba(0,0,0,0.7);"">{BrandingText}</p>
      </div>
    </amp-story-grid-layer>
  </amp-story-page>";
    }

    private static string BuildFactPage(CarFact fact, int factNumber, string bgColor, string imageUrl)
    {
        var factText = TruncateText(fact.Fact, 280);

        var backgroundLayer = !string.IsNullOrEmpty(imageUrl)
            ? $@"    <amp-story-grid-layer template=""fill"">
      <amp-img src=""{Escape(imageUrl)}"" width=""720"" height=""1280"" layout=""fill"" object-fit=""cover""></amp-img>
    </amp-story-grid-layer>
    <amp-story-grid-layer template=""fill"">
      <div style=""background: linear-gradient(0deg, rgba(0,0,0,0.75) 0%, rgba(0,0,0,0.35) 40%, rgba(0,0,0,0.65) 100%); width:100%; height:100%;""></div>
    </amp-story-grid-layer>"
            : $@"    <amp-story-grid-layer template=""fill"">
      <div style=""background: {bgColor}; width:100%; height:100%;""></div>
    </amp-story-grid-layer>";

        return $@"  <amp-story-page id=""fact{factNumber}"">
{backgroundLayer}
    <amp-story-grid-layer template=""thirds"">
      <div grid-area=""upper-third"" style=""display:flex; flex-direction:column; justify-content:flex-end; padding:20px;"">
        <p style=""color:{AccentColor}; font-size:13px; font-family:sans-serif; margin:0; text-shadow: 0 1px 4px rgba(0,0,0,0.7);"">FACT #{factNumber}</p>
        <h2 style=""color:{TextColor}; font-size:22px; font-family:sans-serif; margin:8px 0 0 0; line-height:1.2; text-shadow: 0 2px 6px rgba(0,0,0,0.7);"">{Escape(fact.CatchyTitle)}</h2>
      </div>
      <div grid-area=""middle-third"" style=""display:flex; align-items:flex-start; padding:0 20px;"">
        <p style=""color:{SubtextColor}; font-size:16px; font-family:sans-serif; line-height:1.5; text-shadow: 0 1px 4px rgba(0,0,0,0.7);"">{Escape(factText)}</p>
      </div>
      <div grid-area=""lower-third"" style=""display:flex; align-items:flex-end; justify-content:space-between; padding:20px;"">
        <p style=""color:#aaaaaa; font-size:12px; font-family:sans-serif; text-shadow: 0 1px 3px rgba(0,0,0,0.7);"">{Escape(fact.CarModel)} &middot; {fact.Year}</p>
      </div>
    </amp-story-grid-layer>
  </amp-story-page>";
    }

    private static string BuildCtaPage(string postUrl)
    {
        return $@"  <amp-story-page id=""cta"">
    <amp-story-grid-layer template=""fill"">
      <div style=""background: linear-gradient(135deg, #0f3460 0%, #533483 100%); width:100%; height:100%;""></div>
    </amp-story-grid-layer>
    <amp-story-grid-layer template=""thirds"">
      <div grid-area=""upper-third""></div>
      <div grid-area=""middle-third"" style=""display:flex; flex-direction:column; align-items:center; justify-content:center; padding:20px; text-align:center;"">
        <p style=""color:{TextColor}; font-size:24px; font-family:sans-serif; font-weight:bold;"">Read the full article &#x2192;</p>
        <p style=""color:{SubtextColor}; font-size:14px; font-family:sans-serif; margin-top:12px;"">Swipe up for all the details</p>
      </div>
      <div grid-area=""lower-third"" style=""display:flex; align-items:flex-end; justify-content:center; padding:20px;"">
        <p style=""color:{AccentColor}; font-size:14px; font-family:sans-serif;"">{BrandingText}</p>
      </div>
    </amp-story-grid-layer>
    <amp-story-page-outlink layout=""nodisplay"">
      <a href=""{Escape(postUrl)}"">Read More</a>
    </amp-story-page-outlink>
  </amp-story-page>";
    }

    private static string BuildAutoAdsBlock(string adSenseClientId, string adSenseSlotId)
    {
        if (string.IsNullOrEmpty(adSenseClientId))
            return string.Empty;

        var adAttributes = !string.IsNullOrEmpty(adSenseSlotId)
            ? $@"        ""type"": ""adsense"",
        ""data-ad-client"": ""{Escape(adSenseClientId)}"",
        ""data-ad-slot"": ""{Escape(adSenseSlotId)}"""
            : $@"        ""type"": ""adsense"",
        ""data-ad-client"": ""{Escape(adSenseClientId)}""";

        return $@"  <amp-story-auto-ads>
    <script type=""application/json"">
    {{
      ""ad-attributes"": {{
{adAttributes}
      }}
    }}
    </script>
  </amp-story-auto-ads>
";
    }

    private static string Escape(string text)
    {
        return WebUtility.HtmlEncode(text);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..(maxLength - 1)] + "\u2026";
    }
}
