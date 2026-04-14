namespace CarFacts.Functions.Configuration;

/// <summary>
/// Secret names stored in Azure Key Vault.
/// </summary>
public static class SecretNames
{
    public const string AzureOpenAIApiKey = "AzureOpenAI-ApiKey";
    public const string StabilityAIApiKey = "StabilityAI-ApiKey";
    public const string TogetherAIApiKey = "TogetherAI-ApiKey";
    public const string WordPressOAuthToken = "WordPress-OAuthToken";
    public const string CosmosDbConnectionString = "CosmosDb-ConnectionString";

    // Social media secrets
    public const string TwitterConsumerKey = "Twitter-ConsumerKey";
    public const string TwitterConsumerSecret = "Twitter-ConsumerSecret";
    public const string TwitterAccessToken = "Twitter-AccessToken";
    public const string TwitterAccessTokenSecret = "Twitter-AccessTokenSecret";
    public const string FacebookPageAccessToken = "Facebook-PageAccessToken";
    public const string RedditAppSecret = "Reddit-AppSecret";
    public const string RedditUsername = "Reddit-Username";
    public const string RedditPassword = "Reddit-Password";

    // Pinterest
    public const string PinterestAccessToken = "Pinterest-AccessToken";
}
