using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IFactKeywordStore
{
    Task UpsertFactsAsync(IEnumerable<FactKeywordRecord> records, CancellationToken cancellationToken = default);
}
