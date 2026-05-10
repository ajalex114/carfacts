import asyncio, json, sys, edge_tts

async def main():
    text = "The first speeding ticket was issued in 1902."
    communicate = edge_tts.Communicate(text, voice="en-US-AndrewNeural")
    words = []
    with open("out.mp3", "wb") as audio_file:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_file.write(chunk["data"])
            elif chunk["type"] == "WordBoundary":
                words.append({
                    "word": chunk["text"],
                    "offset_ticks": chunk["offset"],
                    "duration_ticks": chunk["duration"]
                })
    print(json.dumps(words, indent=2))

asyncio.run(main())
