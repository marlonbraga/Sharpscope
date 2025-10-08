using Spectre.Console;

namespace Sharpscope.Cli.Services;

public sealed class SpectreConsoleInteractor : IConsoleInteractor
{
    public void Info(string message) => AnsiConsole.MarkupLine($"[grey]{Escape(message)}[/]");
    public void Success(string message) => AnsiConsole.MarkupLine($"[green]{Escape(message)}[/]");
    public void Error(string message) => AnsiConsole.MarkupLine($"[red]{Escape(message)}[/]");

    public string AskText(string label, string? hint = null, bool allowEmpty = false)
    {
        var prompt = new TextPrompt<string>($"[cyan]{Escape(label)}[/]{Hint(hint)}");

        // Correct Spectre API: AllowEmpty() has no parameters
        if (allowEmpty)
        {
            prompt.AllowEmpty();
        }
        else
        {
            // Enforce non-empty input when not allowed
            prompt.Validate(s =>
                !string.IsNullOrWhiteSpace(s)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("This field is required."));
        }

        return AnsiConsole.Prompt(prompt);
    }

    public string AskChoice(string label, IEnumerable<string> choices, string defaultValue)
    {
        if (choices is null) throw new ArgumentNullException(nameof(choices));

        // SelectionPrompt<T> does NOT support DefaultValue(). Make default the first item.
        var list = choices.ToList();
        if (list.Count == 0)
            throw new ArgumentException("Choices must not be empty.", nameof(choices));

        var idx = list.FindIndex(x => string.Equals(x, defaultValue, StringComparison.OrdinalIgnoreCase));
        if (idx > 0)
        {
            var v = list[idx];
            list.RemoveAt(idx);
            list.Insert(0, v);
        }

        var prompt = new SelectionPrompt<string>()
            .Title($"[cyan]{Escape(label)}[/]")
            .AddChoices(list)
            .HighlightStyle(Style.Parse("yellow"))
            .MoreChoicesText("[grey](arrow keys to navigate)[/]")
            .WrapAround();

        return AnsiConsole.Prompt(prompt);
    }

    public void Raw(string text) => AnsiConsole.WriteLine(text);

    private static string Hint(string? h) =>
        string.IsNullOrWhiteSpace(h) ? string.Empty : $" [grey]({Escape(h)})[/]";

    private static string Escape(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");
}
