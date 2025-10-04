using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Application.DTOs;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Exceptions;
using Sharpscope.Domain.Models;

namespace Sharpscope.Application.UseCases;

/// <summary>
/// Orchestrates: source materialization → language detection → language adapters (CodeModel)
/// → metrics engine → report writers.
/// </summary>
public sealed class AnalyzeSolutionUseCase : IAnalyzeSolutionUseCase
{
    #region Fields

    private readonly ISourceProvider _sourceProvider;
    private readonly ILanguageDetector _languageDetector;
    private readonly IReadOnlyList<ILanguageAdapter> _languageAdapters;
    private readonly IMetricsEngine _metricsEngine;
    private readonly IReadOnlyList<IReportWriter> _reportWriters;

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
        _languageAdapters = (languageAdapters ?? throw new ArgumentNullException(nameof(languageAdapters))).ToList();
        _metricsEngine = metricsEngine ?? throw new ArgumentNullException(nameof(metricsEngine));
        _reportWriters = (reportWriters ?? throw new ArgumentNullException(nameof(reportWriters))).ToList();
    }

    #endregion

    #region Public API

    public async Task<AnalyzeSolutionResult> ExecuteAsync(AnalyzeSolutionRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        ValidateRequest(request);

        // 1) Materialize sources
        var workdir = await MaterializeAsync(request, ct).ConfigureAwait(false);

        // 2) Detect languages
        var languages = await _languageDetector.DetectAsync(workdir, ct).ConfigureAwait(false);
        if (languages.Count == 0)
            throw new SharpscopeException("No supported languages detected in the provided sources.");

        // 3) Build CodeModel(s) with matching adapters
        var models = new List<CodeModel>();
        foreach (var lang in languages)
        {
            var adapter = _languageAdapters.FirstOrDefault(a => a.CanHandle(lang));
            if (adapter is null) continue; // skip unsupported languages, keep it graceful
            var model = await adapter.BuildModelAsync(workdir, ct).ConfigureAwait(false);
            models.Add(model);
        }

        if (models.Count == 0)
            throw new SharpscopeException("No language adapters available for the detected languages.");

        // 4) Merge CodeModels if needed (multi-language)
        var mergedModel = models.Count == 1 ? models[0] : MergeModels(models);

        // 5) Compute metrics
        var metrics = _metricsEngine.Compute(mergedModel);

        // 6) Write reports (may generate multiple formats)
        var outputs = await WriteReportsAsync(metrics, request.Options, ct).ConfigureAwait(false);

        return new AnalyzeSolutionResult(
            WorkDirectory: workdir,
            Metrics: metrics,
            Reports: outputs
        );
    }

    #endregion

    #region Helpers

    private static void ValidateRequest(AnalyzeSolutionRequest req)
    {
        var hasPath = !string.IsNullOrWhiteSpace(req.Path);
        var hasRepo = !string.IsNullOrWhiteSpace(req.RepoUrl);
        if (!hasPath && !hasRepo)
            throw new SharpscopeException("Either Path or RepoUrl must be provided.");

        if (hasPath && hasRepo)
            throw new SharpscopeException("Provide only one of Path or RepoUrl.");
    }

    private async Task<DirectoryInfo> MaterializeAsync(AnalyzeSolutionRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.Path))
            return await _sourceProvider.MaterializeFromLocalAsync(new DirectoryInfo(req.Path!), ct).ConfigureAwait(false);

        return await _sourceProvider.MaterializeFromGitAsync(req.RepoUrl!, ct).ConfigureAwait(false);
    }

    private static CodeModel MergeModels(IReadOnlyList<CodeModel> models)
    {
        // Merge Codebase.Modules (concatenate)
        var allModules = new List<ModuleNode>();
        foreach (var m in models)
            allModules.AddRange(m.Codebase.Modules);

        var codebase = new Codebase(allModules);

        // Merge dependency graphs (union of edges)
        var typeEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var nsEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var m in models)
        {
            UnionEdges(m.DependencyGraph.TypeEdges, typeEdges);
            UnionEdges(m.DependencyGraph.NamespaceEdges, nsEdges);
        }

        var graph = new DependencyGraph(
            typeEdges.ToDictionary(k => k.Key, v => (IReadOnlyCollection<string>)v.Value, StringComparer.Ordinal),
            nsEdges.ToDictionary(k => k.Key, v => (IReadOnlyCollection<string>)v.Value, StringComparer.Ordinal));

        return new CodeModel(codebase, graph);
    }

    private static void UnionEdges(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> source,
        IDictionary<string, HashSet<string>> target)
    {
        foreach (var kv in source)
        {
            if (!target.TryGetValue(kv.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                target[kv.Key] = set;
            }

            if (kv.Value is null) continue;
            foreach (var t in kv.Value)
                set.Add(t);
        }
    }

    private async Task<IReadOnlyList<FileInfo>> WriteReportsAsync(
        MetricsResult metrics,
        AnalyzeSolutionOptions options,
        CancellationToken ct)
    {
        var outputs = new List<FileInfo>();
        if (_reportWriters.Count == 0) return outputs;

        var outDir = ResolveOutputDirectory(options);
        var baseName = string.IsNullOrWhiteSpace(options.OutputFileName)
            ? "sharpscope-report"
            : options.OutputFileName!.Trim();

        foreach (var format in options.Formats)
        {
            var ext = format.ToLowerInvariant();
            var file = new FileInfo(Path.Combine(outDir.FullName, $"{baseName}.{ext}"));
            foreach (var writer in _reportWriters)
            {
                // Writers are expected to ignore unsupported formats or throw;
                // we make a best effort and catch per-writer errors to not fail the whole run.
                try
                {
                    await writer.WriteAsync(metrics, file, format, ct).ConfigureAwait(false);
                    if (!outputs.Any(o => string.Equals(o.FullName, file.FullName, StringComparison.OrdinalIgnoreCase)))
                        outputs.Add(file);
                }
                catch
                {
                    // Intentionally swallow to allow other writers to try.
                    // In production, consider logging via an injected logger.
                }
            }
        }

        return outputs;
    }

    private static DirectoryInfo ResolveOutputDirectory(AnalyzeSolutionOptions opts)
    {
        var dir = string.IsNullOrWhiteSpace(opts.OutputDirectory)
            ? new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()))
            : new DirectoryInfo(opts.OutputDirectory!);

        if (!dir.Exists) dir.Create();
        return dir;
    }

    #endregion
}
