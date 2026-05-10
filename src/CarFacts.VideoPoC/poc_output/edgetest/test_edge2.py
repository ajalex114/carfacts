import asyncio, json, edge_tts

async def main():
    text = "The first speeding ticket was issued in 1902."
    communicate = edge_tts.Communicate(text, voice="en-US-AndrewNeural")
    with open("out.mp3", "wb") as audio_file:
        async for chunk in communicate.stream():
            print("TYPE:", chunk["type"], "KEYS:", list(chunk.keys()))
            if chunk["type"] == "audio":
                audio_file.write(chunk["data"])

asyncio.run(main())
