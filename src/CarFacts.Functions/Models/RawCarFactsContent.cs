using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// Raw car facts content without SEO metadata.
/// Returned by the content generation activity.
/// </summary>
public sealed class RawCarFactsContent
{
    [JsonPropertyName("facts")]
    public List<CarFact> Facts { get; set; } = [];
}
