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
}
