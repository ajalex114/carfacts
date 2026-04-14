namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Pinterest API v5 client for creating pins and managing boards.
/// </summary>
public interface IPinterestService
{
    /// <summary>Creates a pin on the specified board.</summary>
    Task<string> CreatePinAsync(
        string boardId,
        string title,
        string description,
        string link,
        string imageUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Lists all boards for the authenticated user. Returns (boardId, boardName) pairs.</summary>
    Task<List<(string Id, string Name)>> ListBoardsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new board with the given name. Returns the board ID.</summary>
    Task<string> CreateBoardAsync(string name, string description, CancellationToken cancellationToken = default);

    /// <summary>Gets or creates a board by name. Returns the board ID.</summary>
    Task<string> GetOrCreateBoardAsync(string boardName, CancellationToken cancellationToken = default);
}
