namespace CarFacts.VideoFunction.Models;

/// <summary>
/// Rotating narration styles inspired by Jeremy Clarkson's delivery.
/// Each style gives the LLM a different structural/tonal directive so
/// consecutive videos feel distinct even when covering similar cars.
///
/// Styles in rotation (A, B, C, F, G, H):
/// </summary>
public static class NarrationStyles
{
    public static readonly NarrationStyle[] All =
    [
        new("StyleA-GrandStatement",
            "Open with a bold, sweeping claim about the car. Build through one or two vivid details, then land on a grand, definitive conclusion that sounds like the final word on the subject. Authoritative and theatrical."),

        new("StyleB-DismissiveComparison",
            "Hook by briefly dismissing what other cars do, then pivot to show how THIS car is different. Deliver the key facts mid-flow. Close with a punchy line that makes the other cars feel embarrassed by comparison."),

        new("StyleC-SurrenderToIt",
            "Acknowledge how impractical, expensive or irrational the car is — and then reveal you simply don't care. The closer should feel like helpless, joyful surrender. Self-aware but unapologetic."),

        new("StyleF-CinematicMoment",
            "Paint a single vivid moment — the exact second the driver plants their foot, or the first time they hear the engine. Make the listener feel like they are there. Reveal the car's identity through that moment."),

        new("StyleG-ShortAndSavage",
            "Tight, punchy, no wasted words. Open with one hard fact. Build quickly. Close with a single-sentence killer line that hits like a mic drop. This should feel effortless and brutally efficient."),

        new("StyleH-OnPaperTwist",
            "Start with 'On paper...' or a similar setup — list why the car sounds absurd or impractical. Then deliver a sharp twist that completely reverses the argument. The ending should feel inevitable in hindsight."),
    ];

    /// <summary>Returns the style for a given slot index by cycling through All.</summary>
    public static NarrationStyle ForSlot(int slotIndex) => All[slotIndex % All.Length];
}

public record NarrationStyle(string Name, string Instruction);
