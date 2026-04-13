using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GetSocialMediaSettingsActivity
{
    private readonly SocialMediaSettings _settings;

    public GetSocialMediaSettingsActivity(IOptions<SocialMediaSettings> settings)
    {
        _settings = settings.Value;
    }

    [Function(nameof(GetSocialMediaSettingsActivity))]
    public Task<SocialMediaContentSettings> Run(
        [ActivityTrigger] string trigger)
    {
        return Task.FromResult(new SocialMediaContentSettings
        {
            FactsPerDay = _settings.FactsPerDay,
            LinkPostsPerDay = _settings.LinkPostsPerDay
        });
    }
}
