using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Selects the next fact to pin on Pinterest.
/// Prioritizes facts with images that have the lowest pinterestCount.
/// Determines the correct board (default for first pin, category-based for reposts).
/// </summary>
public sealed class SelectPinterestFactActivity
{
    private readonly IFactKeywordStore _factStore;
    private readonly SocialMediaSettings _settings;
    private readonly ILogger<SelectPinterestFactActivity> _logger;

    public SelectPinterestFactActivity(
        IFactKeywordStore factStore,
        IOptions<SocialMediaSettings> settings,
        ILogger<SelectPinterestFactActivity> logger)
    {
        _factStore = factStore;
        _settings = settings.Value;
        _logger = logger;
    }

    [Function(nameof(SelectPinterestFactActivity))]
    public async Task<PinterestFactSelection?> Run(
        [ActivityTrigger] string trigger)
    {
        var candidates = await _factStore.GetFactsForPinterestAsync(50);

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No facts with images available for Pinterest");
            return null;
        }

        // Pick the fact with lowest pinterestCount
        var selected = candidates
            .OrderBy(f => f.PinterestCount)
            .ThenBy(f => f.CreatedAt)
            .First();

        var defaultBoard = _settings.PinterestDefaultBoard;
        if (string.IsNullOrEmpty(defaultBoard))
            defaultBoard = PinterestBoardTaxonomy.DefaultBoard;

        string boardName;
        bool isRepost;

        if (selected.PinterestBoards.Count == 0)
        {
            // First time pinning — use default board
            boardName = defaultBoard;
            isRepost = false;
        }
        else if (!selected.PinterestBoards.Contains(defaultBoard, StringComparer.OrdinalIgnoreCase))
        {
            // Hasn't been pinned to default board yet
            boardName = defaultBoard;
            isRepost = false;
        }
        else
        {
            // Repost — find a relevant category board
            var categoryBoard = PinterestBoardTaxonomy.SelectBoard(
                selected.Title,
                selected.CarModel,
                selected.Keywords,
                selected.Year,
                selected.PinterestBoards);

            if (categoryBoard == null)
            {
                _logger.LogInformation("All boards exhausted for fact {Id} — skipping to next candidate", selected.Id);

                // Try next candidates
                foreach (var candidate in candidates.OrderBy(f => f.PinterestCount).ThenBy(f => f.CreatedAt).Skip(1))
                {
                    if (candidate.PinterestBoards.Count == 0)
                    {
                        selected = candidate;
                        boardName = defaultBoard;
                        isRepost = false;
                        goto found;
                    }

                    var altBoard = PinterestBoardTaxonomy.SelectBoard(
                        candidate.Title, candidate.CarModel, candidate.Keywords,
                        candidate.Year, candidate.PinterestBoards);

                    if (altBoard != null)
                    {
                        selected = candidate;
                        boardName = altBoard;
                        isRepost = true;
                        goto found;
                    }
                }

                _logger.LogWarning("No suitable Pinterest candidates found");
                return null;
            }

            boardName = categoryBoard;
            isRepost = true;
        }

        found:
        _logger.LogInformation("Selected fact {Id} ({Title}) for Pinterest board '{Board}' (repost: {Repost})",
            selected.Id, selected.Title, boardName, isRepost);

        return new PinterestFactSelection
        {
            Fact = selected,
            BoardName = boardName,
            IsRepost = isRepost
        };
    }
}
