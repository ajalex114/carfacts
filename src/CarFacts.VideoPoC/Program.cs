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
var pexelsKey    = config["Pexels:ApiKey"] ?? "";

// ── Car fact (POC: hard-coded) ───────────────────────────────────────────────
// ~50 words → ~20s narration with neural TTS
var carFact =
    "In 1908, Henry Ford introduced the Model T — " +
    "the first car built for ordinary people. " +
    "Ford painted every one black, " +
    "because black paint dried the fastest, keeping the assembly line moving. " +
    "At peak production, a new Model T rolled off the line every 24 seconds. " +
    "It didn't just change driving. It changed the world.";

// ── Output directory ────────────────────────────────────────────────────────
var outputDir    = Path.Combine(Directory.GetCurrentDirectory(), "poc_output");
var clipsDir     = Path.Combine(outputDir, "clips_cache");
Directory.CreateDirectory(outputDir);

var imagePath    = Path.Combine(outputDir, "car.jpg");
var audioPath    = Path.Combine(outputDir, "narration.mp3");
var subtitlePath = Path.Combine(outputDir, "subtitles.ass");
var musicPath    = Path.Combine(outputDir, "music.mp3");
var outputPath   = Path.Combine(outputDir, "carfact_video.mp4");

var useDynamicClips = !string.IsNullOrWhiteSpace(pexelsKey)
                   && pexelsKey != "YOUR_PEXELS_API_KEY";

Console.WriteLine("🚗  CarFacts Video POC");
Console.WriteLine("────────────────────────────────────────");
Console.WriteLine($"Fact  : {carFact}");
Console.WriteLine($"Voice : {voiceName}");
Console.WriteLine($"Clips : {(useDynamicClips ? "Pexels dynamic video" : "static image (add Pexels:ApiKey for video clips)")}");
Console.WriteLine($"Out   : {outputPath}");
Console.WriteLine();

// ── 1. Download fallback image (used when no Pexels key) ────────────────────
if (!useDynamicClips && !File.Exists(imagePath))
{
    Console.Write("📷  Downloading sample car image... ");
    using var http = new HttpClient();
    http.DefaultRequestHeaders.Add("User-Agent", "CarFacts-VideoPoC/1.0");
    var bytes = await http.GetByteArrayAsync(
        "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?w=1080&q=85");
    await File.WriteAllBytesAsync(imagePath, bytes);
    Console.WriteLine("done");
}
else if (!useDynamicClips)
{
    Console.WriteLine("📷  Using cached car.jpg  (delete it to re-download)");
}

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

if (useDynamicClips)
{
    // Plan segments from word timestamps
    var segments = SegmentPlanner.Plan(words, totalDuration, carFact);
    Console.WriteLine($"🗂️   Segments: {segments.Count}");
    foreach (var s in segments)
        Console.WriteLine($"      [{s.StartSeconds:F1}s–{s.EndSeconds:F1}s] → \"{s.SearchQuery}\"");

    // Download & trim clips from Pexels
    Console.WriteLine("⬇️   Fetching clips from Pexels...");
    var pexels   = new PexelsVideoService(pexelsKey, ffmpegExe, clipsDir);
    var resolved = await pexels.ResolveClipsAsync(segments, outputDir);

    // If any clip failed, fall back to single image for that segment
    // (GenerateFromClipsAsync handles null ClipPath by skipping)
    var readyClips = resolved.Where(s => s.ClipPath is not null).ToList();
    if (readyClips.Count == 0)
    {
        Console.WriteLine("⚠️   All clips failed — falling back to static image.");
        useDynamicClips = false;
    }
    else
    {
        Console.Write("🎬  Rendering video with dynamic clips... ");
        await videoGen.GenerateFromClipsAsync(
            readyClips,
            audioPath,
            Path.GetFileName(subtitlePath),
            hasMusic ? musicPath : null,
            outputPath,
            totalDuration);
        Console.WriteLine("done");
    }
}

if (!useDynamicClips)
{
    Console.Write("🎬  Rendering video (static image + Ken Burns)... ");
    await videoGen.GenerateAsync(
        imagePath,
        audioPath,
        Path.GetFileName(subtitlePath),
        hasMusic ? musicPath : null,
        outputPath,
        totalDuration);
    Console.WriteLine("done");
}

Console.WriteLine();
var sizeKb = new FileInfo(outputPath).Length / 1024;
Console.WriteLine($"✅  Output : {outputPath}");
Console.WriteLine($"    Size  : {sizeKb:N0} KB");
Console.WriteLine();
Console.WriteLine("💡  Tips:");
Console.WriteLine("    • Add Pexels:ApiKey to appsettings.json to enable dynamic video clips.");
Console.WriteLine("    • Drop music.mp3 into poc_output/ and re-run to add background audio.");
Console.WriteLine("    • Cached Pexels clips live in poc_output/clips_cache/ — delete to refresh.");

