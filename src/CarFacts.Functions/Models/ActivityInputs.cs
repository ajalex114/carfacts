namespace CarFacts.Functions.Models;

/// <summary>
/// Input for the image upload activity.
/// </summary>
public sealed class UploadImageInput
{
    public GeneratedImage Image { get; set; } = null!;
    public CarFact Fact { get; set; } = null!;
    public int ParentPostId { get; set; }
}

/// <summary>
/// Input for the format-and-publish activity.
/// </summary>
public sealed class PublishInput
{
    public RawCarFactsContent Content { get; set; } = null!;
    public SeoMetadata Seo { get; set; } = null!;
    public List<UploadedMedia> Media { get; set; } = [];
    public string TodayDate { get; set; } = string.Empty;
    public int DraftPostId { get; set; }
    public List<BacklinkSuggestion> Backlinks { get; set; } = [];
    public List<RelatedPostSuggestion> RelatedPosts { get; set; } = [];
}

/// <summary>
/// Input for a social media publish activity.
/// </summary>
public sealed class SocialPublishInput
{
    public string Teaser { get; set; } = string.Empty;
    public string PostUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = [];
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Input for the fact keywords storage activity.
/// </summary>
public sealed class StoreFactKeywordsInput
{
    public RawCarFactsContent Content { get; set; } = null!;
    public SeoMetadata Seo { get; set; } = null!;
    public string PostUrl { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public List<BacklinkSuggestion> Backlinks { get; set; } = [];
    public List<RelatedPostSuggestion> RelatedPosts { get; set; } = [];
    public List<UploadedMedia> Media { get; set; } = [];
    public string PostTitle { get; set; } = string.Empty;
}

/// <summary>
/// Input for the find-backlinks activity.
/// </summary>
public sealed class FindBacklinksInput
{
    public List<FactKeywordEntry> FactKeywords { get; set; } = [];
    public string CurrentPostUrl { get; set; } = string.Empty;
}

/// <summary>
/// Combined result from backlink + related post lookups.
/// </summary>
public sealed class BacklinksResult
{
    public List<BacklinkSuggestion> Backlinks { get; set; } = [];
    public List<RelatedPostSuggestion> RelatedPosts { get; set; } = [];
}
