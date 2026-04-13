using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Serilog;

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Debug();

var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
if (!isAzure)
{
    var logDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
    Directory.CreateDirectory(logDirectory);
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    logConfig.WriteTo.File(
        Path.Combine(logDirectory, $"carfacts-{timestamp}.log"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
}

Log.Logger = logConfig.CreateLogger();

var hostBuilder = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        var settings = config.Build();
        var appConfigEndpoint = settings["AppConfiguration__Endpoint"];
        if (!string.IsNullOrEmpty(appConfigEndpoint))
        {
            config.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
                    .TrimKeyPrefix("")
                    .Select("*");
            });
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        RegisterSettings(context, services);
        RegisterServices(context, services);
    });

if (!isAzure)
    hostBuilder.UseSerilog();

var host = hostBuilder.Build();

host.Run();

static void RegisterSettings(HostBuilderContext context, IServiceCollection services)
{
    var config = context.Configuration;

    services.Configure<AISettings>(config.GetSection(AISettings.SectionName));
    services.Configure<KeyVaultSettings>(config.GetSection(KeyVaultSettings.SectionName));
    services.Configure<StabilityAISettings>(config.GetSection(StabilityAISettings.SectionName));
    services.Configure<TogetherAISettings>(config.GetSection(TogetherAISettings.SectionName));
    services.Configure<WordPressSettings>(config.GetSection(WordPressSettings.SectionName));
    services.Configure<ScheduleSettings>(config.GetSection(ScheduleSettings.SectionName));
    services.Configure<SocialMediaSettings>(config.GetSection(SocialMediaSettings.SectionName));
    services.Configure<CosmosDbSettings>(config.GetSection(CosmosDbSettings.SectionName));
    services.Configure<MediumSettings>(config.GetSection(MediumSettings.SectionName));
}

static void RegisterServices(HostBuilderContext context, IServiceCollection services)
{
    var config = context.Configuration;

    var isLocal = string.Equals(
        config["AZURE_FUNCTIONS_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);

    // Secret provider
    if (isLocal)
        services.AddSingleton<ISecretProvider, LocalSecretProvider>();
    else
        services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

    // Semantic Kernel — text provider
    RegisterTextProvider(config, services, isLocal);

    // Image provider
    RegisterImageProvider(config, services, isLocal);

    // Content generation (uses SK's IChatCompletionService)
    services.AddSingleton<IContentGenerationService, ContentGenerationService>();
    services.AddSingleton<ISeoGenerationService, SeoGenerationService>();

    // Other services
    services.AddSingleton<IContentFormatterService, ContentFormatterService>();
    services.AddHttpClient<IWordPressService, WordPressService>();

    // Social media services
    services.AddHttpClient<TwitterService>();
    services.AddSingleton<ISocialMediaService>(sp => sp.GetRequiredService<TwitterService>());
    services.AddHttpClient<FacebookService>();
    services.AddSingleton<ISocialMediaService>(sp => sp.GetRequiredService<FacebookService>());
    services.AddHttpClient<RedditService>();
    services.AddSingleton<ISocialMediaService>(sp => sp.GetRequiredService<RedditService>());
    services.AddSingleton<SocialMediaPublisher>();

    // Medium publishing service
    services.AddHttpClient<MediumService>();
    services.AddSingleton<IMediumService>(sp => sp.GetRequiredService<MediumService>());

    // Cosmos DB for fact keyword storage
    RegisterCosmosDb(config, services, isLocal);
}

static void RegisterTextProvider(
    Microsoft.Extensions.Configuration.IConfiguration config,
    IServiceCollection services,
    bool isLocal)
{
    var provider = config["AI:TextProvider"] ?? "AzureOpenAI";
    string apiKey;
    if (isLocal)
    {
        apiKey = config["Secrets:AzureOpenAI-ApiKey"] ?? "";
    }
    else
    {
        var vaultUri = config["KeyVault:VaultUri"] ?? "";
        var client = new Azure.Security.KeyVault.Secrets.SecretClient(
            new Uri(vaultUri), new Azure.Identity.DefaultAzureCredential());
        apiKey = client.GetSecret("AzureOpenAI-ApiKey").Value.Value;
    }

    var kernelBuilder = Kernel.CreateBuilder();

    switch (provider)
    {
        case "OpenAI":
            var openAiKey = isLocal ? config["Secrets:OpenAI-ApiKey"] ?? "" : apiKey;
            var openAiModel = config["AI:OpenAIModelId"] ?? "gpt-4o-mini";
            kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiKey);
            break;

        case "AzureOpenAI":
        default:
            var endpoint = config["AI:AzureOpenAIEndpoint"] ?? "";
            var deployment = config["AI:AzureOpenAIDeploymentName"] ?? "gpt-4o-mini";
            kernelBuilder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
            break;
    }

    var kernel = kernelBuilder.Build();

    // Register the Kernel and extract IChatCompletionService from it
    services.AddSingleton(kernel);
    services.AddSingleton(kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
}

static void RegisterImageProvider(
    Microsoft.Extensions.Configuration.IConfiguration config,
    IServiceCollection services,
    bool isLocal)
{
    var provider = config["AI:ImageProvider"] ?? "StabilityAI";

    if (isLocal)
    {
        // Local: use single provider with caching
        switch (provider)
        {
            case "TogetherAI":
                services.AddHttpClient<TogetherAIImageGenerationService>();
                services.AddSingleton<IImageGenerationService>(sp =>
                    new CachedImageGenerationService(
                        sp.GetRequiredService<TogetherAIImageGenerationService>(),
                        sp.GetRequiredService<ILogger<CachedImageGenerationService>>()));
                break;

            case "None":
                break;

            case "StabilityAI":
            default:
                services.AddHttpClient<ImageGenerationService>();
                services.AddSingleton<IImageGenerationService, CachedImageGenerationService>();
                break;
        }
    }
    else
    {
        // Production: fallback chain (StabilityAI → TogetherAI → empty)
        services.AddHttpClient<ImageGenerationService>();
        services.AddHttpClient<TogetherAIImageGenerationService>();
        services.AddSingleton<IImageGenerationService>(sp =>
            new FallbackImageGenerationService(
                new IImageGenerationService[]
                {
                    sp.GetRequiredService<ImageGenerationService>(),
                    sp.GetRequiredService<TogetherAIImageGenerationService>()
                },
                sp.GetRequiredService<ILogger<FallbackImageGenerationService>>()));
    }
}

static void RegisterCosmosDb(
    Microsoft.Extensions.Configuration.IConfiguration config,
    IServiceCollection services,
    bool isLocal)
{
    string connectionString;
    if (isLocal)
    {
        connectionString = config["Secrets:CosmosDb-ConnectionString"] ?? "";
    }
    else
    {
        var vaultUri = config["KeyVault:VaultUri"] ?? "";
        var client = new Azure.Security.KeyVault.Secrets.SecretClient(
            new Uri(vaultUri), new Azure.Identity.DefaultAzureCredential());
        try
        {
            connectionString = client.GetSecret("CosmosDb-ConnectionString").Value.Value;
        }
        catch
        {
            connectionString = "";
        }
    }

    if (!string.IsNullOrEmpty(connectionString))
    {
        services.AddSingleton(new Microsoft.Azure.Cosmos.CosmosClient(connectionString,
            new Microsoft.Azure.Cosmos.CosmosClientOptions
            {
                SerializerOptions = new Microsoft.Azure.Cosmos.CosmosSerializationOptions
                {
                    PropertyNamingPolicy = Microsoft.Azure.Cosmos.CosmosPropertyNamingPolicy.CamelCase
                }
            }));
        services.AddSingleton<IFactKeywordStore, CosmosFactKeywordStore>();
        services.AddSingleton<ISocialMediaQueueStore, CosmosSocialMediaQueueStore>();
    }
    else
    {
        services.AddSingleton<IFactKeywordStore, NullFactKeywordStore>();
        services.AddSingleton<ISocialMediaQueueStore, NullSocialMediaQueueStore>();
    }
}
