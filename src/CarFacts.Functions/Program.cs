using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Serilog;

var logDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
Directory.CreateDirectory(logDirectory);

var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        Path.Combine(logDirectory, $"carfacts-{timestamp}.log"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        RegisterSettings(context, services);
        RegisterServices(context, services);
    })
    .Build();

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

    // Other services
    services.AddSingleton<IContentFormatterService, ContentFormatterService>();
    services.AddHttpClient<IWordPressService, WordPressService>();
}

static void RegisterTextProvider(
    Microsoft.Extensions.Configuration.IConfiguration config,
    IServiceCollection services,
    bool isLocal)
{
    var provider = config["AI:TextProvider"] ?? "AzureOpenAI";
    var apiKey = isLocal ? config["Secrets:AzureOpenAI-ApiKey"] ?? "" : "";

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

    switch (provider)
    {
        case "TogetherAI":
            if (isLocal)
            {
                services.AddHttpClient<TogetherAIImageGenerationService>();
                services.AddSingleton<IImageGenerationService>(sp =>
                    new CachedImageGenerationService(
                        sp.GetRequiredService<TogetherAIImageGenerationService>(),
                        sp.GetRequiredService<ILogger<CachedImageGenerationService>>()));
            }
            else
            {
                services.AddHttpClient<IImageGenerationService, TogetherAIImageGenerationService>();
            }
            break;

        case "None":
            break;

        case "StabilityAI":
        default:
            if (isLocal)
            {
                services.AddHttpClient<ImageGenerationService>();
                services.AddSingleton<IImageGenerationService, CachedImageGenerationService>();
            }
            else
            {
                services.AddHttpClient<IImageGenerationService, ImageGenerationService>();
            }
            break;
    }
}
