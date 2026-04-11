using System.Reflection;

namespace CarFacts.Functions.Prompts;

public static class PromptLoader
{
    private static readonly Assembly Assembly = typeof(PromptLoader).Assembly;

    public static string LoadSystemPrompt()
    {
        return LoadResource("CarFacts.Functions.Prompts.SystemPrompt.txt");
    }

    public static string LoadUserPrompt(string todayDate)
    {
        var template = LoadResource("CarFacts.Functions.Prompts.UserPrompt.txt");
        return template.Replace("{{DATE}}", todayDate);
    }

    public static string LoadSeoSystemPrompt()
    {
        return LoadResource("CarFacts.Functions.Prompts.SeoSystemPrompt.txt");
    }

    public static string LoadSeoUserPrompt(string contentSummary)
    {
        var template = LoadResource("CarFacts.Functions.Prompts.SeoUserPrompt.txt");
        return template.Replace("{{CONTENT}}", contentSummary);
    }

    public static string LoadTweetFactsSystemPrompt()
    {
        return LoadResource("CarFacts.Functions.Prompts.TweetFactsSystemPrompt.txt");
    }

    public static string LoadTweetFactsUserPrompt()
    {
        return LoadResource("CarFacts.Functions.Prompts.TweetFactsUserPrompt.txt");
    }

    public static string LoadTweetLinkPrompt(string postTitle, string postUrl)
    {
        var template = LoadResource("CarFacts.Functions.Prompts.TweetLinkPrompt.txt");
        return template.Replace("{{POST_TITLE}}", postTitle).Replace("{{POST_URL}}", postUrl);
    }

    private static string LoadResource(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
