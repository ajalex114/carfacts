namespace CarFacts.Functions.Models;

public sealed class GeneratedImage
{
    public int FactIndex { get; set; }
    public byte[] ImageData { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
}
