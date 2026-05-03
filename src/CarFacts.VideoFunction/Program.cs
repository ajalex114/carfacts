using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // FfmpegManager shared across activities — caches binary after first download
        services.AddSingleton(_ => new FfmpegManager(
            cfg["Storage:ConnectionString"] ?? throw new InvalidOperationException("Storage:ConnectionString not configured")));

        // ImageQueryExtractorService — extracts clean Bing search query from fact text
        services.AddSingleton(_ => new ImageQueryExtractorService(
            cfg["OpenAI:Endpoint"],
            cfg["OpenAI:ApiKey"],
            cfg["OpenAI:DeploymentName"]));

        // CarFactGenerationService — generates ~50-word car fact via LLM (Step 0)
        services.AddSingleton(_ => new CarFactGenerationService(
            cfg["OpenAI:Endpoint"],
            cfg["OpenAI:ApiKey"],
            cfg["OpenAI:DeploymentName"]));

        // VideoTrackingService — writes/reads published-video entries in Cosmos DB
        services.AddSingleton<VideoTrackingService>(sp =>
            new VideoTrackingService(
                cfg["CosmosDB:AccountEndpoint"],
                sp.GetRequiredService<ILogger<VideoTrackingService>>()));

        // VideoScheduleService — writes daily video schedule to Cosmos DB video-schedule container
        services.AddSingleton<VideoScheduleService>(sp =>
            new VideoScheduleService(
                cfg["CosmosDB:AccountEndpoint"],
                sp.GetRequiredService<ILogger<VideoScheduleService>>()));

        // TTS + subtitle services used by SynthesizeTtsActivity
        services.AddSingleton(_ => new TtsService(
            cfg["Speech:Key"]       ?? throw new InvalidOperationException("Speech:Key not configured"),
            cfg["Speech:Region"]    ?? "centralindia",
            cfg["Speech:VoiceName"] ?? "en-US-AndrewNeural"));

        services.AddSingleton(new SubtitleGenerator());

        // NewsService — fetches recent automotive RSS news for brand-aware fact generation
        services.AddSingleton<NewsService>(sp =>
            new NewsService(sp.GetRequiredService<ILogger<NewsService>>()));

        // IConfiguration needed by HttpStartFunction to read connection strings at runtime
        services.AddSingleton(cfg);
    })
    .Build();

host.Run();
