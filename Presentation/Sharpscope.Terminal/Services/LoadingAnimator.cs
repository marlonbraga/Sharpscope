using Spectre.Console;

namespace Sharpscope.Cli.Services;

public sealed class LoadingAnimator : ILoadingAnimator
{
    public async Task StartAsync(string label, CancellationToken token)
    {
        if (Console.IsOutputRedirected)
        {
            try { await Task.Delay(Timeout.Infinite, token); } catch { /* canceled */ }
            return;
        }

        var frames = Enumerable.Range(1, 12).Select(i => new string('●', i)).ToArray();

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        int line;
        try
        {
            line = Console.CursorTop - 1;
        }
        catch
        {
            try { await Task.Delay(Timeout.Infinite, token); } catch { /* canceled */ }
            return;
        }
        var i = 0;

        while (!token.IsCancellationRequested)
        {
            var frame = frames[i % frames.Length];
            var text = $"{label}: {frame}";
            try
            {
                AnsiConsole.Cursor.SetPosition(0, line);
                AnsiConsole.Markup($"[grey]{Escape(text)}[/]   ");
            }
            catch
            {
                try { await Task.Delay(Timeout.Infinite, token); } catch { /* canceled */ }
                return;
            }
            i++;
            try { await Task.Delay(200, token); } catch { /* canceled */ }
        }
    }

    private static string Escape(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");
}
