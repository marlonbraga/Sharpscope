using Spectre.Console;

namespace Sharpscope.Cli.Services;

public sealed class LoadingAnimator : ILoadingAnimator
{
    public async Task StartAsync(string label, CancellationToken token)
    {
        var frames = Enumerable.Range(1, 12).Select(i => new string('●', i)).ToArray();

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        var line = Console.CursorTop - 1;
        var i = 0;

        while (!token.IsCancellationRequested)
        {
            var frame = frames[i % frames.Length];
            var text = $"{label}: {frame}";
            AnsiConsole.Cursor.SetPosition(0, line);
            AnsiConsole.Markup($"[grey]{Escape(text)}[/]   ");
            i++;
            try { await Task.Delay(200, token); } catch { /* canceled */ }
        }
    }

    private static string Escape(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");
}
