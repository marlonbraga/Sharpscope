using Sharpscope.Cli.Infrastructure;
using Sharpscope.Cli.Services;
using Sharpscope.Application.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Sharpscope.Cli.Commands;

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    private readonly AnalyzeSolutionUseCase _useCase;
    private readonly IConsoleInteractor _console;
    private readonly IInputNormalizer _normalizer;
    private readonly ILoadingAnimator _loading;

    private static readonly HashSet<string> SupportedFormats =
        new(StringComparer.OrdinalIgnoreCase) { "json", "md", "csv", "sarif" };

    public AnalyzeCommand(
        AnalyzeSolutionUseCase useCase,
        IConsoleInteractor console,
        IInputNormalizer normalizer,
        ILoadingAnimator loading)
    {
        _useCase = useCase;
        _console = console;
        _normalizer = normalizer;
        _loading = loading;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings s)
    {
        var interactive = IsEmpty(s);
        if (interactive)
            s = await PromptAllAsync();

        // Normalize source (treat path/repo with the same rules)
        var (path, repo) = _normalizer.NormalizeSource(s.Path, s.Repo);
        if (path is null && repo is null)
        {
            _console.Error("You must provide a valid local path or a Git repository URL. Exiting.");
            return -1;
        }

        // Normalize format (default json)
        var format = NormalizeFormat(s.Format);
        // Normalize print (default false)
        var print = NormalizePrint(s.Print);
        // Keep file only when --out is provided
        var keepFile = !string.IsNullOrWhiteSpace(s.OutputPath);

        // Fake loading animation while executing use case
        var cts = new CancellationTokenSource();
        var animTask = _loading.StartAsync("ANALISING CODE", cts.Token);

        FileInfo outputFile;
        try
        {
            var req = new AnalyzeRequest(
                Path: path,
                RepoUrl: repo,
                Format: format,
                OutputPath: s.OutputPath // may be null
            );

            outputFile = await _useCase.ExecuteAsync(req, CancellationToken.None);
        }
        finally
        {
            cts.Cancel();
            await animTask;
            AnsiConsole.WriteLine(); // spacing after animation
        }

        await HandleOutputAsync(outputFile, keepFile, print);
        return 0;
    }

    private static bool IsEmpty(AnalyzeSettings s) =>
        string.IsNullOrWhiteSpace(s.Path) &&
        string.IsNullOrWhiteSpace(s.Repo) &&
        string.IsNullOrWhiteSpace(s.Format) &&
        string.IsNullOrWhiteSpace(s.OutputPath) &&
        string.IsNullOrWhiteSpace(s.Print);

    private async Task<AnalyzeSettings> PromptAllAsync()
    {
        _console.Info("[Interactive] Please answer the prompts below. Defaults are shown.");

        // Single “source” experience: user may paste either a local folder or a repo URL.
        var source = _console.AskText(
            "Source (local path or Git URL):",
            hint: "Required. Example: C:\\repo OR https://github.com/org/repo",
            allowEmpty: false);

        // Format with default json
        var format = _console.AskChoice(
            "Format (default: json):",
            new[] { "json", "md", "csv", "sarif" },
            defaultValue: "json");

        // Output optional
        var outFile = _console.AskText(
            "Out (optional, path to save file):",
            hint: "Leave empty to NOT persist the file",
            allowEmpty: true);

        // Print with default false
        var print = _console.AskChoice(
            "Print to console? (default: false):",
            new[] { "false", "true" },
            defaultValue: "false");

        // We keep both properties available. Normalizer will decide later.
        // If the user typed a source, put it in both to leverage normalization:
        return new AnalyzeSettings
        {
            Path = source,
            Repo = source,
            Format = format,
            OutputPath = string.IsNullOrWhiteSpace(outFile) ? null : outFile,
            Print = print
        };
    }

    private static string NormalizeFormat(string? raw)
    {
        var f = (raw ?? "json").Trim().ToLowerInvariant();
        return SupportedFormats.Contains(f) ? f : "json";
    }

    private static bool NormalizePrint(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    private async Task HandleOutputAsync(FileInfo file, bool keepFile, bool print)
    {
        string? content = null;

        if (print || !keepFile)
            content = await File.ReadAllTextAsync(file.FullName);

        if (print)
            _console.Raw(content ?? string.Empty);

        if (keepFile)
            _console.Success($"Report generated: {file.FullName}");
        else
        {
            try { if (file.Exists) file.Delete(); } catch { /* ignore */ }
        }
    }
}
