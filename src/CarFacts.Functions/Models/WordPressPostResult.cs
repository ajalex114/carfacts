namespace CarFacts.Functions.Models;

public sealed class WordPressPostResult
{
    public int PostId { get; set; }
    public string PostUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}
