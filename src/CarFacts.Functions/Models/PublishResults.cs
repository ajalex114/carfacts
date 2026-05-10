namespace CarFacts.Functions.Models;

/// <summary>
/// Result of uploading a single image to Azure Blob Storage.
/// </summary>
public sealed class BlobUploadResult
{
    public int FactIndex { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
}

/// <summary>
/// Result returned by FormatAndPublishActivity — the formatted HTML content.
/// </summary>
public sealed class FormatAndPublishResult
{
    public string HtmlContent { get; set; } = string.Empty;
}
