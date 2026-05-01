using CarFacts.Functions.Helpers;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Saves the full PostDocument to the Cosmos DB 'posts' container.
/// This is the SWA's canonical source of truth.
/// </summary>
public sealed class SavePostToCosmosActivity
{
    private readonly IPostStore _postStore;
    private readonly ILogger<SavePostToCosmosActivity> _logger;

    public SavePostToCosmosActivity(
        IPostStore postStore,
        ILogger<SavePostToCosmosActivity> logger)
    {
        _postStore = postStore;
        _logger = logger;
    }

    [Function(nameof(SavePostToCosmosActivity))]
    public async Task<bool> Run([ActivityTrigger] SavePostInput input)
    {
        _logger.LogInformation("Saving PostDocument to Cosmos DB for slug '{Slug}'", input.Slug);

        var partitionKey = input.PublishedAt.ToString("yyyy-MM");
        var id = $"{input.PublishedAt:yyyy-MM-dd}_{input.Slug}";

        var images = input.BlobResults.Select(b =>
        {
            var fact = input.Content.Facts.Count > b.FactIndex ? input.Content.Facts[b.FactIndex] : null;
            return new PostImage
            {
                FactIndex = b.FactIndex,
                BlobUrl = b.BlobUrl,
                BlobPath = b.BlobPath,
                AltText = b.AltText,
                Title = fact != null ? $"{fact.CarModel} ({fact.Year})" : string.Empty,
                Caption = fact?.CatchyTitle ?? string.Empty
            };
        }).ToList();

        var featuredImageUrl = images.FirstOrDefault()?.BlobUrl ?? string.Empty;

        var post = new PostDocument
        {
            Id = id,
            PartitionKey = partitionKey,
            Slug = input.Slug,
            PostUrl = input.PostUrl,
            Title = input.Seo.MainTitle,
            MetaDescription = input.Seo.MetaDescription,
            Excerpt = input.Seo.SocialMediaTeaser,
            HtmlContent = input.HtmlContent,
            FeaturedImageUrl = featuredImageUrl,
            Images = images,
            Keywords = input.Seo.Keywords,
            Tags = input.Seo.Keywords.Take(5).ToList(),
            SocialHashtags = input.Seo.SocialMediaHashtags,
            Category = "car-facts",
            Author = "thecargeek",
            PublishedAt = input.PublishedAt,
            GeoSummary = input.Seo.GeoSummary,
            Facts = input.Content.Facts,
            WordPressPostId = input.WordPressPostId,
            WordPressPostUrl = input.WordPressPostUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _postStore.SavePostAsync(post);

        _logger.LogInformation("Saved PostDocument: id={Id}, url={Url}", post.Id, post.PostUrl);
        return true;
    }
}
