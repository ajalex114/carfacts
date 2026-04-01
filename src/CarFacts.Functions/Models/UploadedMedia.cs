namespace CarFacts.Functions.Models;

public sealed class UploadedMedia
{
    public int FactIndex { get; set; }
    public int MediaId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
}
