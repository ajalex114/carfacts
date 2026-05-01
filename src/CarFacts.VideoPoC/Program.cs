using CarFacts.VideoPoC.Models;
using CarFacts.VideoPoC.Services;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "CARFACTS_")
    .Build();

// ── Config ──────────────────────────────────────────────────────────────────
var speechKey    = config["Speech:Key"] ?? "";
var speechRegion = config["Speech:Region"] ?? "eastus";
var voiceName    = config["Speech:VoiceName"] ?? "en-US-AndrewNeural";
var websiteUrl   = config["Video:WebsiteUrl"] ?? "carfactsdaily.com";
var ffmpegPath   = config["Video:FfmpegPath"] ?? "ffmpeg";
var pythonPath   = config["Tts:PythonPath"] ?? "python";
var ffmpegDir    = config["Tts:FfmpegDir"] ?? "";

// LLM-based image query extraction (falls back to regex if OpenAI not configured)
var queryExtractor = new ImageQueryExtractorService(
    config["OpenAI:Endpoint"],
    config["OpenAI:ApiKey"],
    config["OpenAI:DeploymentName"]);

// ── Car fact — LLM-generated ─────────────────────────────────────────────────
var factService = new CarFacts.VideoPoC.Services.CarFactGenerationService(
    config["OpenAI:Endpoint"],
    config["OpenAI:ApiKey"],
    config["OpenAI:DeploymentName"]);

Console.Write("🤖  Generating car fact via LLM... ");
var carFact = await factService.GenerateFactAsync();
Console.WriteLine("done.");

// ── Output directory ────────────────────────────────────────────────────────
var outputDir    = Path.Combine(Directory.GetCurrentDirectory(), "poc_output");
Directory.CreateDirectory(outputDir);

var audioPath    = Path.Combine(outputDir, "narration.mp3");
var subtitlePath = Path.Combine(outputDir, "subtitles.ass");
var musicPath    = Path.Combine(outputDir, "music.mp3");
var outputPath   = Path.Combine(outputDir, "carfact_video.mp4");

Console.WriteLine("🚗  CarFacts Video POC");
Console.WriteLine("────────────────────────────────────────");
Console.WriteLine($"Fact  : {carFact}");
Console.WriteLine($"Query : (LLM/regex extract from fact)");
Console.WriteLine($"Voice : {voiceName}");
Console.WriteLine($"Clips : Bing images + Ken Burns (Wikimedia fallback)");
Console.WriteLine($"Out   : {outputPath}");
Console.WriteLine();

// ── 1. Extract image search query from fact (LLM or regex fallback) ─────────
Console.Write("🔍  Extracting image search query... ");
var imageSearchQuery = await queryExtractor.ExtractQueryAsync(carFact);
Console.WriteLine($"\"{imageSearchQuery}\"");

// ── 2. TTS narration + word timestamps ──────────────────────────────────────
List<WordTiming> words;

if (!string.IsNullOrWhiteSpace(speechKey) && speechKey != "YOUR_AZURE_SPEECH_KEY")
{
    Console.Write("🎙️   Synthesizing narration (Azure Speech)... ");
    var tts = new TtsService(speechKey, speechRegion, voiceName);
    words = await tts.SynthesizeAsync(carFact, audioPath);
}
else
{
    var scriptPath = Path.Combine(AppContext.BaseDirectory, "tts_timing.py");
    if (File.Exists(scriptPath))
    {
        Console.Write("🎙️   Synthesizing narration (edge-tts + Whisper)... ");
        var tts = new EdgeTtsService(pythonPath, scriptPath, ffmpegDir, voiceName);
        words = await tts.SynthesizeAsync(carFact, audioPath);
    }
    else
    {
        Console.Write("🎙️   Synthesizing narration (Windows built-in TTS)... ");
        audioPath = Path.Combine(outputDir, "narration.wav");
        var tts = new WindowsTtsService();
        words = await tts.SynthesizeAsync(carFact, audioPath);
    }
}
Console.WriteLine($"done  ({words.Count} words)");

// ── 3. Compute total duration ────────────────────────────────────────────────
var narrationEnd  = words[^1].EndSeconds;
var totalDuration = narrationEnd + 2.3;
Console.WriteLine($"⏱️   Duration: {totalDuration:F1} s  (narration ends at {narrationEnd:F1} s)");

// ── 4. ASS subtitles ─────────────────────────────────────────────────────────
Console.Write("📝  Generating subtitles... ");
var subGen  = new SubtitleGenerator();
var assText = subGen.GenerateAss(words, totalDuration, websiteUrl);
await File.WriteAllTextAsync(subtitlePath, assText, System.Text.Encoding.UTF8);
Console.WriteLine("done");

// ── 5. Resolve video source ──────────────────────────────────────────────────
var hasMusic = File.Exists(musicPath);
Console.WriteLine($"🎵  Background music: {(hasMusic ? "yes" : "not found — drop music.mp3 into poc_output/ to enable")}");

var ffmpegExe = ffmpegPath == "ffmpeg" && !string.IsNullOrEmpty(ffmpegDir)
    ? Path.Combine(ffmpegDir, "ffmpeg.exe")
    : ffmpegPath;

var videoGen = new VideoGenerator(ffmpegExe);

// Plan segments from word timestamps
var segments = SegmentPlanner.Plan(words, totalDuration, carFact, imageSearchQuery);
Console.WriteLine($"🗂️   Segments: {segments.Count}");
foreach (var s in segments)
    Console.WriteLine($"      [{s.StartSeconds:F1}s–{s.EndSeconds:F1}s] → \"{s.SearchQuery}\"");

// Download images and apply Ken Burns effect
Console.WriteLine("⬇️   Fetching images (Bing / Wikimedia fallback)...");
var imageService = new ImageKenBurnsService(ffmpegExe);
var resolved     = await imageService.ResolveClipsAsync(segments, outputDir);
Console.WriteLine();

var readyClips = resolved.Where(s => s.ClipPath is not null).ToList();
if (readyClips.Count == 0)
{
    Console.WriteLine("⚠️   All image clips failed — aborting.");
    return;
}

Console.Write("🎬  Rendering video with image clips... ");
await videoGen.GenerateFromClipsAsync(
    readyClips,
    audioPath,
    Path.GetFileName(subtitlePath),
    hasMusic ? musicPath : null,
    outputPath,
    totalDuration);
Console.WriteLine("done");

Console.WriteLine();
var sizeKb = new FileInfo(outputPath).Length / 1024;
Console.WriteLine($"✅  Output : {outputPath}");
Console.WriteLine($"    Size  : {sizeKb:N0} KB");
Console.WriteLine();
Console.WriteLine("💡  Tips:");
Console.WriteLine("    • Drop music.mp3 into poc_output/ and re-run to add background audio.");
Console.WriteLine("    • Change Video:ImageSearchQuery in appsettings.json to search different images.");

