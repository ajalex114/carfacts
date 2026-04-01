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

    private static string LoadResource(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
