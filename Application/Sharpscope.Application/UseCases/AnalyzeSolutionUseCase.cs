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
    private readonly IIntegrationDiscoveryEngine _integrations;

    #endregion

    #region Ctor

    public AnalyzeSolutionUseCase(
        ISourceProvider sourceProvider,
        ILanguageDetector languageDetector,
        IEnumerable<ILanguageAdapter> languageAdapters,
        IMetricsEngine metricsEngine,
        IIntegrationDiscoveryEngine integrations)
    {
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _languageDetector = languageDetector ?? throw new ArgumentNullException(nameof(languageDetector));
        _languageAdapters = languageAdapters ?? throw new ArgumentNullException(nameof(languageAdapters));
        _metricsEngine = metricsEngine ?? throw new ArgumentNullException(nameof(metricsEngine));
        _integrations = integrations ?? throw new ArgumentNullException(nameof(integrations));
    }

    #endregion

    #region API

    /// <summary>
    /// Materializes the source (local or git), detects language, builds the code graph, runs metrics and returns snapshot.
    /// </summary>
    public async Task<AnalysisSnapshot> ExecuteAsync(AnalyzeRequest request, CancellationToken ct)
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

        // 4) Build graph (async)
        var graph = await adapter.BuildGraphAsync(workdir, ct).ConfigureAwait(false);

        // 5) Compute metrics (sync)
        var metrics = _metricsEngine.Compute(graph);

        // 6) Discover integrations (async)
        var integrations = await _integrations.DiscoverAsync(graph, workdir, ct).ConfigureAwait(false);

        var metadata = new AnalysisMetadata(
            RepoUrlOrPath: request.Path ?? request.RepoUrl ?? workdir.FullName,
            CommitSha: null,
            Branch: null,
            TimestampUtc: DateTimeOffset.UtcNow,
            ToolVersion: ResolveToolVersion(),
            MetricsSchemaVersion: "1",
            IntegrationsSchemaVersion: "1"
        );

        return new AnalysisSnapshot(
            Metadata: metadata,
            Graph: graph,
            Metrics: metrics,
            Integrations: integrations
        );
    }

    #endregion

    #region Helpers

    private static void ValidateRequest(AnalyzeRequest r)
    {
        var hasPath = !string.IsNullOrWhiteSpace(r.Path);
        var hasRepo = !string.IsNullOrWhiteSpace(r.RepoUrl);

        if (hasPath == hasRepo) // both true or both false
            throw new ArgumentException("You must provide either Path or RepoUrl (but not both).");

        // Format/OutputPath are handled by presentation layers (CLI/API)
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

    private static string ResolveToolVersion()
        => typeof(AnalyzeSolutionUseCase).Assembly.GetName().Version?.ToString() ?? "unknown";

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
