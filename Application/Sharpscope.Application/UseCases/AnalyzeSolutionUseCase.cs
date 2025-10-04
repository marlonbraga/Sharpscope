using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Application.UseCases;

/// <summary>
/// Orchestrates: source materialization (local/git) → language detection → model building → metrics → report.
/// </summary>
public sealed class AnalyzeSolutionUseCase
{
    #region Dependencies

    private readonly ISourceProvider _sourceProvider;
    private readonly ILanguageDetector _languageDetector;
    private readonly IEnumerable<ILanguageAdapter> _languageAdapters;
    private readonly IMetricsEngine _metricsEngine;
    private readonly IEnumerable<IReportWriter> _reportWriters;

    #endregion

    #region Ctor

    public AnalyzeSolutionUseCase(
        ISourceProvider sourceProvider,
        ILanguageDetector languageDetector,
        IEnumerable<ILanguageAdapter> languageAdapters,
        IMetricsEngine metricsEngine,
        IEnumerable<IReportWriter> reportWriters)
    {
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _languageDetector = languageDetector ?? throw new ArgumentNullException(nameof(languageDetector));
        _languageAdapters = languageAdapters ?? throw new ArgumentNullException(nameof(languageAdapters));
        _metricsEngine = metricsEngine ?? throw new ArgumentNullException(nameof(metricsEngine));
        _reportWriters = reportWriters ?? throw new ArgumentNullException(nameof(reportWriters));
    }

    #endregion

    #region API

    /// <summary>
    /// Materializes the source (local or git), detects language, builds the code model, runs metrics and writes the report.
    /// Returns the output file info.
    /// </summary>
    public async Task<FileInfo> ExecuteAsync(AnalyzeRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        ValidateRequest(request);

        // 1) Materialize
        var workdir = await MaterializeAsync(request, ct).ConfigureAwait(false);

        // 2) Detect language (async)
        var language = await _languageDetector.DetectLanguageAsync(workdir, ct).ConfigureAwait(false)
                       ?? throw new NotSupportedException("Could not detect a supported language in the provided source.");

        // 3) Resolve adapter
        var adapter = ResolveAdapter(language);

        // 4) Build model (async)
        var model = await adapter.BuildModelAsync(workdir, ct).ConfigureAwait(false);

        // 5) Compute metrics (sync)
        var metrics = _metricsEngine.Compute(model);

        // 6) Resolve writer + output file
        var writer = ResolveWriter(request.Format);
        var output = ResolveOutputFile(workdir, request.OutputPath, writer.Format);

        // 7) Write (async)
        await writer.WriteAsync(metrics, output, ct).ConfigureAwait(false);

        return output;
    }

    #endregion

    #region Helpers

    private static void ValidateRequest(AnalyzeRequest r)
    {
        var hasPath = !string.IsNullOrWhiteSpace(r.Path);
        var hasRepo = !string.IsNullOrWhiteSpace(r.RepoUrl);

        if (hasPath == hasRepo) // both true or both false
            throw new ArgumentException("You must provide either Path or RepoUrl (but not both).");

        if (string.IsNullOrWhiteSpace(r.Format))
            throw new ArgumentException("Format is required.", nameof(r.Format));
    }

    private async Task<DirectoryInfo> MaterializeAsync(AnalyzeRequest r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Path))
        {
            var di = new DirectoryInfo(r.Path!);
            return await _sourceProvider.MaterializeFromLocalAsync(di, ct).ConfigureAwait(false);
        }
        else
        {
            return await _sourceProvider.MaterializeFromGitAsync(r.RepoUrl!, ct).ConfigureAwait(false);
        }
    }

    private ILanguageAdapter ResolveAdapter(string language)
    {
        var adapter = _languageAdapters.FirstOrDefault(a => a.CanHandle(language));
        if (adapter is null)
        {
            var supported = string.Join(", ", _languageAdapters.Select(a => a.LanguageId));
            throw new NotSupportedException($"No adapter found for language '{language}'. Supported: {supported}");
        }
        return adapter;
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

    private static FileInfo ResolveOutputFile(DirectoryInfo workdir, string? outputPath, string format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return new FileInfo(outputPath);

        var name = $"sharpscope-report.{format.ToLowerInvariant()}";
        return new FileInfo(Path.Combine(workdir.FullName, name));
    }

    #endregion
}

/// <summary>
/// Request DTO for AnalyzeSolutionUseCase.
/// </summary>
public sealed record AnalyzeRequest(
    string? Path,
    string? RepoUrl,
    string Format,
    string? OutputPath
);
