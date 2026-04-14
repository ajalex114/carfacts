namespace CarFacts.Functions.Configuration;

public sealed class AISettings
{
    public const string SectionName = "AI";

    public string TextProvider { get; set; } = "AzureOpenAI"; // AzureOpenAI, OpenAI
    public string ImageProvider { get; set; } = "StabilityAI"; // StabilityAI, TogetherAI, None

    // Azure OpenAI settings
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;
    public string AzureOpenAIDeploymentName { get; set; } = "gpt-4o-mini";

    // OpenAI settings (when using OpenAI directly)
    public string OpenAIModelId { get; set; } = "gpt-4o-mini";

    // Shared settings
    public double Temperature { get; set; } = 0.85;
}

public sealed class StabilityAISettings
{
    public const string SectionName = "StabilityAI";

    public string BaseUrl { get; set; } = "https://api.stability.ai";
    public string Model { get; set; } = "stable-diffusion-xl-1024-v1-0";
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 30;
    public int CfgScale { get; set; } = 7;
}

public sealed class TogetherAISettings
{
    public const string SectionName = "TogetherAI";

    public string Model { get; set; } = "black-forest-labs/FLUX.1.1-pro";
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 20;
}

public sealed class WordPressSettings
{
    public const string SectionName = "WordPress";

    public string SiteId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PostStatus { get; set; } = "publish";
    public bool EmbedImagesAsBase64 { get; set; } = false;
    public bool SkipImages { get; set; } = false;
}

public sealed class KeyVaultSettings
{
    public const string SectionName = "KeyVault";

    public string VaultUri { get; set; } = string.Empty;
}

public sealed class ScheduleSettings
{
    public const string SectionName = "Schedule";

    public string CronExpression { get; set; } = "0 0 6 * * *";
}

public sealed class CosmosDbSettings
{
    public const string SectionName = "CosmosDb";

    public string DatabaseName { get; set; } = "carfacts";
    public string ContainerName { get; set; } = "fact-keywords";
}

public sealed class SocialMediaSettings
{
    public const string SectionName = "SocialMedia";

    // Per-platform toggles
    public bool TwitterEnabled { get; set; }
    public bool FacebookEnabled { get; set; }
    public bool RedditEnabled { get; set; }

    // Content generation counts (per day per platform)
    public int FactsPerDay { get; set; } = 5;
    public int LinkPostsPerDay { get; set; } = 1;

    // Posting schedule (NCRONTAB: every 4 hours by default)
    public string PostingCronExpression { get; set; } = "0 0 */4 * * *";

    // Facebook
    public List<string> FacebookPageIds { get; set; } = [];

    // Reddit
    public string RedditAppId { get; set; } = string.Empty;
    public string RedditUserAgent { get; set; } = "CarFacts/1.0";
    public List<string> RedditSubreddits { get; set; } = [];

    // Pinterest
    public bool PinterestEnabled { get; set; }
    public string PinterestDefaultBoard { get; set; } = "Car Facts";
    public string PinterestPostingCronExpression { get; set; } = "0 0 1,6,10,15,19,21 * * *";
}
