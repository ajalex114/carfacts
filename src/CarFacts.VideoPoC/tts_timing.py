"""
TTS + Word Timing via edge-tts (neural voice) + Whisper (forced alignment)

Usage:
    python tts_timing.py <text> <voice> <output_mp3> <output_json> <ffmpeg_dir>

Outputs:
    <output_mp3>  — neural TTS audio
    <output_json> — JSON array: [{"word": "...", "start": 0.000, "end": 0.320}, ...]
"""
import asyncio, json, sys, os, edge_tts, whisper

def main():
    if len(sys.argv) < 6:
        print("Usage: tts_timing.py <text> <voice> <output_mp3> <output_json> <ffmpeg_dir>", file=sys.stderr)
        sys.exit(1)

    text       = sys.argv[1]
    voice      = sys.argv[2]
    out_mp3    = sys.argv[3]
    out_json   = sys.argv[4]
    ffmpeg_dir = sys.argv[5]

    # Inject ffmpeg into PATH so Whisper can load audio (needed in sandboxed Python envs)
    if ffmpeg_dir:
        os.environ["PATH"] = ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")

    # ── 1. Generate neural audio with edge-tts ──────────────────────────────────
    async def generate():
        communicate = edge_tts.Communicate(text, voice=voice)
        with open(out_mp3, "wb") as f:
            async for chunk in communicate.stream():
                if chunk["type"] == "audio":
                    f.write(chunk["data"])

    asyncio.run(generate())

    # ── 2. Word-level timestamps via Whisper forced alignment ───────────────────
    model  = whisper.load_model("tiny")
    result = model.transcribe(out_mp3, word_timestamps=True, language="en")

    words = []
    for seg in result["segments"]:
        for w in seg.get("words", []):
            word = w["word"].strip()
            if word:
                words.append({
                    "word":  word,
                    "start": round(w["start"], 3),
                    "end":   round(w["end"],   3)
                })

    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(words, f, indent=2)

    # Print summary for C# to verify
    if words:
        print(f"ok:{len(words)}:{words[-1]['end']:.3f}")
    else:
        print("error:no_words_detected", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
