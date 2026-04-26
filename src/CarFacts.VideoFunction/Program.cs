using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        // YtDlpManager — same cold-start pattern as FfmpegManager (~12 MB binary)
        services.AddSingleton(_ => new YtDlpManager(
            cfg["Storage:ConnectionString"] ?? throw new InvalidOperationException("Storage:ConnectionString not configured")));

        // TTS + subtitle services used by SynthesizeTtsActivity
        services.AddSingleton(_ => new TtsService(
            cfg["Speech:Key"]       ?? throw new InvalidOperationException("Speech:Key not configured"),
            cfg["Speech:Region"]    ?? "centralindia",
            cfg["Speech:VoiceName"] ?? "en-US-AndrewNeural"));

        services.AddSingleton(new SubtitleGenerator());

        // IConfiguration needed by HttpStartFunction to read connection strings at runtime
        services.AddSingleton(cfg);
    })
    .Build();

host.Run();
