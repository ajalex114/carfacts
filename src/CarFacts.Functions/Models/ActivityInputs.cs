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

/// <summary>
/// Input for the social media content generation orchestrator.
/// </summary>
public sealed class SocialMediaOrchestratorInput
{
    public string PostUrl { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
    public int FactsPerDay { get; set; } = 5;
    public int LinkPostsPerDay { get; set; } = 1;
    public bool LikesEnabled { get; set; } = true;
    public bool RepliesEnabled { get; set; } = true;
    public int LikesPerDayMin { get; set; } = 10;
    public int LikesPerDayMax { get; set; } = 20;
    public int RepliesPerDayMin { get; set; } = 3;
    public int RepliesPerDayMax { get; set; } = 6;
}

/// <summary>
/// Input for the generate-tweet-link activity.
/// </summary>
public sealed class GenerateTweetLinkInput
{
    public string PostUrl { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
    public int LinkCount { get; set; } = 1;
}

/// <summary>
/// Input for the store-social-queue activity.
/// </summary>
public sealed class StoreSocialQueueInput
{
    public List<TweetFactResult> Facts { get; set; } = [];
    public List<TweetLinkResult> LinkTweets { get; set; } = [];
    public List<string> EnabledPlatforms { get; set; } = [];
    public bool LikesEnabled { get; set; } = true;
    public bool RepliesEnabled { get; set; } = true;
    public int LikesPerDayMin { get; set; } = 10;
    public int LikesPerDayMax { get; set; } = 20;
    public int RepliesPerDayMin { get; set; } = 3;
    public int RepliesPerDayMax { get; set; } = 6;
}

/// <summary>
/// Result from generating standalone tweet facts.
/// </summary>
public sealed class TweetFactResult
{
    public string Text { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = [];
}

/// <summary>
/// Result from generating a blog post link tweet.
/// </summary>
public sealed class TweetLinkResult
{
    public string Text { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = [];
    public string PostUrl { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
}

/// <summary>
/// Settings for social media content generation counts.
/// </summary>
public sealed class SocialMediaContentSettings
{
    public int FactsPerDay { get; set; } = 5;
    public int LinkPostsPerDay { get; set; } = 1;
    public bool LikesEnabled { get; set; } = true;
    public bool RepliesEnabled { get; set; } = true;
    public int LikesPerDayMin { get; set; } = 10;
    public int LikesPerDayMax { get; set; } = 20;
    public int RepliesPerDayMin { get; set; } = 3;
    public int RepliesPerDayMax { get; set; } = 6;
}

/// <summary>
/// Input for the increment-social-counts activity.
/// </summary>
public sealed class IncrementSocialCountsInput
{
    public string PostUrl { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Input for the GeneratePinContent activity.
/// </summary>
public sealed class GeneratePinContentInput
{
    public string Title { get; set; } = string.Empty;
    public string CarModel { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<string> Keywords { get; set; } = [];
}

/// <summary>
/// Result from GeneratePinContent activity.
/// </summary>
public sealed class PinContent
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Input for the CreatePinterestPin activity.
/// </summary>
public sealed class CreatePinterestPinInput
{
    public string BoardName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// Input for the UpdatePinterestTracking activity.
/// </summary>
public sealed class UpdatePinterestTrackingInput
{
    public string RecordId { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
}

/// <summary>
/// Input for the ScheduledPostOrchestrator — contains a single queue item to post at its scheduled time.
/// </summary>
public sealed class ScheduledPostInput
{
    public string ItemId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "fact";
    public string Activity { get; set; } = "post";
    public string? PostUrl { get; set; }
    public string? PostTitle { get; set; }
    public string? ReplyToTweetId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
}

/// <summary>
/// Result from generating a tweet reply.
/// </summary>
public sealed class TweetReplyResult
{
    public string TweetId { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string ReplyText { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
}

/// <summary>
/// Result from finding a tweet to like.
/// </summary>
public sealed class TweetLikeResult
{
    public string TweetId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
}

/// <summary>
/// Result from the Pinterest fact selection activity.
/// </summary>
public sealed class PinterestFactSelection
{
    public FactKeywordRecord Fact { get; set; } = null!;
    public string BoardName { get; set; } = string.Empty;
    public bool IsRepost { get; set; }
}

/// <summary>
/// Input for the Web Story creation activity.
/// </summary>
public sealed class CreateWebStoryInput
{
    public List<CarFact> Facts { get; set; } = [];
    public string MainTitle { get; set; } = string.Empty;
    public string PostUrl { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string FeaturedImageUrl { get; set; } = string.Empty;
}
