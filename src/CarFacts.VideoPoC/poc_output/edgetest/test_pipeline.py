import asyncio, json, sys, os, edge_tts, whisper

# Inject ffmpeg path for Windows Store Python sandbox
FFMPEG_DIR = r"C:\Users\alenalex\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1-full_build\bin"
os.environ["PATH"] = FFMPEG_DIR + ";" + os.environ.get("PATH", "")

async def generate_audio(text, voice, output_mp3):
    communicate = edge_tts.Communicate(text, voice=voice)
    with open(output_mp3, "wb") as f:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                f.write(chunk["data"])

def get_word_timings(mp3_path):
    model = whisper.load_model("tiny")
    result = model.transcribe(mp3_path, word_timestamps=True, language="en")
    words = []
    for seg in result["segments"]:
        for w in seg.get("words", []):
            words.append({
                "word": w["word"].strip(),
                "start": round(w["start"], 3),
                "end": round(w["end"], 3)
            })
    return words

text = "The first speeding ticket was issued in 1902, for driving 45 miles per hour. The speed limit back then? Just eight miles per hour."
voice = "en-US-AndrewNeural"
mp3_path = "test_neural.mp3"

asyncio.run(generate_audio(text, voice, mp3_path))
print("Audio generated.", flush=True)

words = get_word_timings(mp3_path)
print(json.dumps(words, indent=2))
