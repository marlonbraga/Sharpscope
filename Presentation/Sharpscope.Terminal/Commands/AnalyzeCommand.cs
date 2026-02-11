using Sharpscope.Cli.Infrastructure;
using Sharpscope.Cli.Services;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Sharpscope.Cli.Commands;

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    private readonly AnalyzeSolutionUseCase _useCase;
    private readonly IEnumerable<IReportWriter> _reportWriters;
    private readonly IConsoleInteractor _console;
    private readonly IInputNormalizer _normalizer;
    private readonly ILoadingAnimator _loading;

    private static readonly HashSet<string> SupportedFormats =
        new(StringComparer.OrdinalIgnoreCase) { "json", "md", "csv", "sarif" };
    private static readonly HashSet<string> SupportedProfiles =
        new(StringComparer.OrdinalIgnoreCase) { "work" };

    public AnalyzeCommand(
        AnalyzeSolutionUseCase useCase,
        IEnumerable<IReportWriter> reportWriters,
        IConsoleInteractor console,
        IInputNormalizer normalizer,
        ILoadingAnimator loading)
    {
        _useCase = useCase;
        _reportWriters = reportWriters;
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
        var (path, repo) = _normalizer.NormalizeSource(s.Path ?? s.Solution, s.Repo);
        if (path is null && repo is null)
        {
            _console.Error("You must provide a valid local path or a Git repository URL. Exiting.");
            return -1;
        }

        // Normalize format (default json)
        var format = NormalizeFormat(s.Format);
        // Normalize print (default false)
        var print = NormalizePrint(s.Print);
        // Normalize integration profile (default work)
        var profile = NormalizeProfile(s.Profile);
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
                OutputPath: s.OutputPath, // may be null
                IntegrationProfile: profile
            );

            var snapshot = await _useCase.ExecuteAsync(req, CancellationToken.None);
            var writer = ResolveWriter(format);
            outputFile = ResolveOutputFile(s.OutputPath, writer.Format);
            await writer.WriteAsync(snapshot, outputFile, CancellationToken.None);
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
        string.IsNullOrWhiteSpace(s.Solution) &&
        string.IsNullOrWhiteSpace(s.Repo) &&
        string.IsNullOrWhiteSpace(s.Format) &&
        string.IsNullOrWhiteSpace(s.OutputPath) &&
        string.IsNullOrWhiteSpace(s.Print) &&
        string.IsNullOrWhiteSpace(s.Profile);

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

        var profile = _console.AskChoice(
            "Integration profile (default: work):",
            new[] { "work" },
            defaultValue: "work");

        // We keep both properties available. Normalizer will decide later.
        // If the user typed a source, put it in both to leverage normalization:
        return new AnalyzeSettings
        {
            Path = source,
            Repo = source,
            Format = format,
            OutputPath = string.IsNullOrWhiteSpace(outFile) ? null : outFile,
            Print = print,
            Profile = profile
        };
    }

    private static string NormalizeFormat(string? raw)
    {
        var f = (raw ?? "json").Trim().ToLowerInvariant();
        return SupportedFormats.Contains(f) ? f : "json";
    }

    private static bool NormalizePrint(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeProfile(string? raw)
    {
        var profile = (raw ?? "work").Trim().ToLowerInvariant();
        return SupportedProfiles.Contains(profile) ? profile : "work";
    }

    private IReportWriter ResolveWriter(string format)
    {
        var writer = _reportWriters.FirstOrDefault(w => string.Equals(w.Format, format, StringComparison.OrdinalIgnoreCase));
        if (writer is null)
        {
            var supported = string.Join(", ", _reportWriters.Select(w => w.Format));
            throw new NotSupportedException($"Unknown report format '{format}'. Supported: {supported}");
        }
        return writer;
    }

    private static FileInfo ResolveOutputFile(string? outputPath, string format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return new FileInfo(outputPath);

        var name = $"sharpscope-report.{format.ToLowerInvariant()}";
        return new FileInfo(Path.Combine(Environment.CurrentDirectory, name));
    }

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

