import asyncio, json, edge_tts

async def main():
    text = "The first speeding ticket was issued in 1902."
    communicate = edge_tts.Communicate(text, voice="en-US-AndrewNeural")
    submaker = edge_tts.SubMaker()
    with open("out.mp3", "wb") as audio_file:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_file.write(chunk["data"])
            submaker.feed(chunk)
    
    # Print subtitle info
    print("Offsets:", submaker.offsets[:10])
    print("Durations:", submaker.durations[:10])
    print("Words:", submaker.words[:10])

asyncio.run(main())
