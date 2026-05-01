using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

public sealed class CarFact
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("catchy_title")]
    public string CatchyTitle { get; set; } = string.Empty;

    [JsonPropertyName("fact")]
    public string Fact { get; set; } = string.Empty;

    [JsonPropertyName("car_model")]
    public string CarModel { get; set; } = string.Empty;

    [JsonPropertyName("image_prompt")]
    public string ImagePrompt { get; set; } = string.Empty;

    [JsonPropertyName("image_search_query")]
    public string ImageSearchQuery { get; set; } = string.Empty;
}
