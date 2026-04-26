using CarFacts.VideoPoC.Models;
using System.Text;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// Generates an ASS subtitle file with rolling 3-word karaoke display:
/// [dim prev] [YELLOW current] [dim next]
/// Plus a constant watermark and a 2-second website hook at the end.
/// </summary>
public class SubtitleGenerator
{
    // ASS color format: &HAABBGGRR  (AA=alpha 00=opaque, then BGR channels)
    private const string Yellow = "&H0000FFFF";  // active word
    private const string Gray   = "&H00AAAAAA";  // inactive flanking words
    private const string White  = "&H00FFFFFF";

    public string GenerateAss(List<WordTiming> words, double totalDuration, string websiteUrl)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("PlayResX: 1080");
        sb.AppendLine("PlayResY: 1920");
        sb.AppendLine("WrapStyle: 0");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

        // Karaoke: bold, large, thick black outline — sits in lower third
        sb.AppendLine($"Style: Karaoke,Arial,86,{White},{White},&H00000000,&H00000000,1,0,0,0,100,100,0,0,1,4,2,2,60,60,260,1");

        // Hook: medium text with dark backing box — bottom center, last 2 seconds
        sb.AppendLine($"Style: Hook,Arial,50,{White},{White},&H00000000,&H90000000,1,0,0,0,100,100,0,0,3,0,0,2,60,60,80,1");

        // Watermark: ghost-style overlay
        // Font ~60pt so rendered text ≈ half the frame width (540px of 1080px)
        // ASS color: &HAABBGGRR — alpha CC (~80% transparent), white = FFFFFF
        sb.AppendLine($"Style: Watermark,Arial,60,&HCCFFFFFF,&HCCFFFFFF,&HCC000000,&H00000000,0,0,0,0,100,100,2,0,1,2,0,6,0,0,0,1");

        sb.AppendLine();
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        // Watermark — always visible
        // \an6 = right-anchored mid-line; x=1050 (30px from right edge); y=1280 (1/3 from bottom)
        sb.AppendLine($"Dialogue: 0,{Ts(0)},{Ts(totalDuration)},Watermark,,0,0,0,,{{\\an6\\pos(1050,1280)}}{Esc(websiteUrl)}");

        // Rolling karaoke: for each word, show [prev dim] [curr yellow] [next dim]
        for (int i = 0; i < words.Count; i++)
        {
            var curr = words[i];
            var prev = i > 0 ? words[i - 1] : null;
            var next = i < words.Count - 1 ? words[i + 1] : null;

            // Hold until the next word starts to avoid any gap/flicker between entries
            var lineEnd = next?.StartSeconds ?? curr.EndSeconds + 0.05;

            var line = new StringBuilder();
            if (prev != null)
                line.Append($"{{\\c{Gray}}}{Esc(prev.Word)} ");

            line.Append($"{{\\c{Yellow}}}{Esc(curr.Word)}");

            if (next != null)
                line.Append($" {{\\c{Gray}}}{Esc(next.Word)}");

            line.Append($"{{\\c{White}}}"); // reset colour

            sb.AppendLine($"Dialogue: 0,{Ts(curr.StartSeconds)},{Ts(lineEnd)},Karaoke,,0,0,0,,{line}");
        }

        // Website hook — last 2 seconds only
        var hookStart = Math.Max(totalDuration - 2.0, 0);
        sb.AppendLine($"Dialogue: 1,{Ts(hookStart)},{Ts(totalDuration)},Hook,,0,0,0,,{{\\an2}}Follow at {Esc(websiteUrl)}");

        return sb.ToString();
    }

    // Format seconds as ASS timestamp  H:MM:SS.cs
    private static string Ts(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
    }

    private static string Esc(string text) =>
        text.Replace("{", "\\{").Replace("}", "\\}").Replace("\n", "\\N");
}
