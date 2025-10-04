using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Application.DTOs;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Exceptions;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.ApplicationTests;

public sealed class AnalyzeSolutionUseCaseTests
{
    #region Success paths

    [Fact(DisplayName = "ExecuteAsync with local path and one supported language generates reports and returns metrics")]
    public async Task ExecuteAsync_Path_SingleLanguage_Succeeds()
    {
        // Arrange
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();
        var adapter = Substitute.For<ILanguageAdapter>();
        var engine = Substitute.For<IMetricsEngine>();
        var writer1 = Substitute.For<IReportWriter>();
        var writer2 = Substitute.For<IReportWriter>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "csharp" }));

        adapter.LanguageId.Returns("csharp");
        adapter.CanHandle("csharp").Returns(true);
        var model = DummyModel("M");
        adapter.BuildModelAsync(workdir, Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(model));

        var metrics = DummyMetricsResult();
        engine.Compute(Arg.Any<CodeModel>()).Returns(metrics);

        // Writers succeed (no-ops)
        writer1.WriteAsync(metrics, Arg.Any<FileInfo>(), "json", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);
        writer1.WriteAsync(metrics, Arg.Any<FileInfo>(), "md", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);
        writer2.WriteAsync(metrics, Arg.Any<FileInfo>(), "json", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);
        writer2.WriteAsync(metrics, Arg.Any<FileInfo>(), "md", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider,
            languageDetector,
            new[] { adapter },
            engine,
            new[] { writer1, writer2 });

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\fake\\path",
            Options = new AnalyzeSolutionOptions
            {
                Formats = new[] { "json", "md" },
                OutputDirectory = Path.GetTempPath(),
                OutputFileName = "report"
            }
        };

        // Act
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.WorkDirectory.FullName.ShouldBe(workdir.FullName);
        result.Metrics.ShouldBeSameAs(metrics);
        // Two formats -> two output files (deduped even with 2 writers)
        result.Reports.Count.ShouldBe(2);
        result.Reports.Select(f => f.Extension.TrimStart('.')).OrderBy(s => s)
              .ShouldBe(new[] { "json", "md" });
    }

    [Fact(DisplayName = "ExecuteAsync with repo URL and multiple detected languages: processes only supported adapters")]
    public async Task ExecuteAsync_Repo_MultiLanguage_UnsupportedSkipped()
    {
        // Arrange
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();
        var csharpAdapter = Substitute.For<ILanguageAdapter>();
        var engine = Substitute.For<IMetricsEngine>();
        var writer = Substitute.For<IReportWriter>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromGitAsync("https://example/repo.git", Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "csharp", "python" }));

        csharpAdapter.LanguageId.Returns("csharp");
        csharpAdapter.CanHandle("csharp").Returns(true);
        csharpAdapter.CanHandle("python").Returns(false);
        csharpAdapter.BuildModelAsync(workdir, Arg.Any<CancellationToken>())
                     .Returns(Task.FromResult(DummyModel("OnlyCSharp")));

        var metrics = DummyMetricsResult();
        engine.Compute(Arg.Any<CodeModel>()).Returns(metrics);

        writer.WriteAsync(metrics, Arg.Any<FileInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.CompletedTask);

        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider,
            languageDetector,
            new[] { csharpAdapter }, // no python adapter
            engine,
            new[] { writer });

        var request = new AnalyzeSolutionRequest
        {
            RepoUrl = "https://example/repo.git",
            Options = new AnalyzeSolutionOptions { Formats = new[] { "json" } }
        };

        // Act
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Metrics.ShouldBeSameAs(metrics);
        result.Reports.Count.ShouldBe(1);
        result.Reports.First().Extension.ShouldBe(".json");
    }

    #endregion

    #region Validation & error paths

    [Fact(DisplayName = "ExecuteAsync throws when neither Path nor RepoUrl is provided")]
    public async Task ExecuteAsync_NoPathNoRepo_Throws()
    {
        var useCase = new AnalyzeSolutionUseCase(
            Substitute.For<ISourceProvider>(),
            Substitute.For<ILanguageDetector>(),
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());

        var request = new AnalyzeSolutionRequest { Options = new AnalyzeSolutionOptions() };

        await Should.ThrowAsync<SharpscopeException>(() => useCase.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact(DisplayName = "ExecuteAsync throws when both Path and RepoUrl are provided")]
    public async Task ExecuteAsync_BothPathAndRepo_Throws()
    {
        var useCase = new AnalyzeSolutionUseCase(
            Substitute.For<ISourceProvider>(),
            Substitute.For<ILanguageDetector>(),
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\x",
            RepoUrl = "https://example/repo.git",
            Options = new AnalyzeSolutionOptions()
        };

        await Should.ThrowAsync<SharpscopeException>(() => useCase.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact(DisplayName = "ExecuteAsync throws when no supported languages are detected")]
    public async Task ExecuteAsync_NoLanguages_Throws()
    {
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()));

        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider,
            languageDetector,
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\fake\\path",
            Options = new AnalyzeSolutionOptions()
        };

        await Should.ThrowAsync<SharpscopeException>(() => useCase.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact(DisplayName = "ExecuteAsync throws when languages are detected but no adapter is available")]
    public async Task ExecuteAsync_LanguageButNoAdapter_Throws()
    {
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "csharp" }));

        // No adapters provided:
        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider,
            languageDetector,
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\fake\\path",
            Options = new AnalyzeSolutionOptions()
        };

        await Should.ThrowAsync<SharpscopeException>(() => useCase.ExecuteAsync(request, CancellationToken.None));
    }

    #endregion

    #region Merging & writer failure handling

    [Fact(DisplayName = "ExecuteAsync merges CodeModels from multiple adapters and passes a merged model to MetricsEngine")]
    public async Task ExecuteAsync_MultiAdapter_MergesModels()
    {
        // Arrange
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();
        var adapterA = Substitute.For<ILanguageAdapter>();
        var adapterB = Substitute.For<ILanguageAdapter>();
        var engine = Substitute.For<IMetricsEngine>();
        var writer = Substitute.For<IReportWriter>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "langA", "langB" }));

        adapterA.CanHandle("langA").Returns(true);
        adapterA.CanHandle("langB").Returns(false);
        adapterA.LanguageId.Returns("langA");

        adapterB.CanHandle("langB").Returns(true);
        adapterB.CanHandle("langA").Returns(false);
        adapterB.LanguageId.Returns("langB");

        var modelA = DummyModel("A", ("A.T1", "A.T2"), nsEdge: ("A.N1", "A.N2"));
        var modelB = DummyModel("B", ("B.T3", "B.T4"), nsEdge: ("B.N1", "B.N2"));
        adapterA.BuildModelAsync(workdir, Arg.Any<CancellationToken>()).Returns(Task.FromResult(modelA));
        adapterB.BuildModelAsync(workdir, Arg.Any<CancellationToken>()).Returns(Task.FromResult(modelB));

        CodeModel? capturedMerged = null;
        var metrics = DummyMetricsResult();
        engine.Compute(Arg.Do<CodeModel>(cm => capturedMerged = cm)).Returns(metrics);

        writer.WriteAsync(metrics, Arg.Any<FileInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.CompletedTask);

        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider, languageDetector, new[] { adapterA, adapterB }, engine, new[] { writer });

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\fake\\path",
            Options = new AnalyzeSolutionOptions { Formats = new[] { "json" } }
        };

        // Act
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        // Assert merged characteristics
        capturedMerged.ShouldNotBeNull();
        capturedMerged!.Codebase.Modules.Count.ShouldBe(2); // A + B
        // Union of edges: expect to see both A and B edges
        capturedMerged.DependencyGraph.TypeEdges.Keys.ShouldContain("A.T1");
        capturedMerged.DependencyGraph.TypeEdges.Keys.ShouldContain("B.T3");
        capturedMerged.DependencyGraph.NamespaceEdges.Keys.ShouldContain("A.N1");
        capturedMerged.DependencyGraph.NamespaceEdges.Keys.ShouldContain("B.N1");

        result.Metrics.ShouldBeSameAs(metrics);
    }

    [Fact(DisplayName = "ExecuteAsync continues when one report writer fails for a format")]
    public async Task ExecuteAsync_WriterFailure_IsSwallowed()
    {
        var sourceProvider = Substitute.For<ISourceProvider>();
        var languageDetector = Substitute.For<ILanguageDetector>();
        var adapter = Substitute.For<ILanguageAdapter>();
        var engine = Substitute.For<IMetricsEngine>();
        var writerOk = Substitute.For<IReportWriter>();
        var writerFails = Substitute.For<IReportWriter>();

        var workdir = new DirectoryInfo(Path.GetTempPath());
        sourceProvider.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(workdir));

        languageDetector.DetectAsync(workdir, Arg.Any<CancellationToken>())
                        .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "csharp" }));

        adapter.LanguageId.Returns("csharp");
        adapter.CanHandle("csharp").Returns(true);
        adapter.BuildModelAsync(workdir, Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(DummyModel("M")));

        var metrics = DummyMetricsResult();
        engine.Compute(Arg.Any<CodeModel>()).Returns(metrics);

        // OK writer
        writerOk.WriteAsync(metrics, Arg.Any<FileInfo>(), "json", Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        // Failing writer for the same format
        writerFails.WriteAsync(metrics, Arg.Any<FileInfo>(), "json", Arg.Any<CancellationToken>())
                   .Returns<Task>(_ => throw new InvalidOperationException("boom"));

        var useCase = new AnalyzeSolutionUseCase(
            sourceProvider,
            languageDetector,
            new[] { adapter },
            engine,
            new[] { writerOk, writerFails });

        var request = new AnalyzeSolutionRequest
        {
            Path = "C:\\fake\\path",
            Options = new AnalyzeSolutionOptions
            {
                Formats = new[] { "json" },
                OutputDirectory = Path.GetTempPath()
            }
        };

        // Act
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        // Assert: even with one writer failing, we still have the JSON report from the other writer
        result.Reports.Count.ShouldBe(1);
        result.Reports[0].Extension.ShouldBe(".json");
    }

    #endregion

    #region Helpers

    private static CodeModel DummyModel(
        string moduleName,
        (string src, string dst)? typeEdge = null,
        (string src, string dst)? nsEdge = null)
    {
        var ns = new NamespaceNode($"{moduleName}.N", new List<TypeNode>());
        var module = new ModuleNode(moduleName, new List<NamespaceNode> { ns });
        var codebase = new Codebase(new List<ModuleNode> { module });

        var typeEdges = new Dictionary<string, IReadOnlyCollection<string>>();
        if (typeEdge.HasValue)
            typeEdges[typeEdge.Value.src] = new HashSet<string> { typeEdge.Value.dst };

        var nsEdges = new Dictionary<string, IReadOnlyCollection<string>>();
        if (nsEdge.HasValue)
            nsEdges[nsEdge.Value.src] = new HashSet<string> { nsEdge.Value.dst };

        var graph = new DependencyGraph(typeEdges, nsEdges);
        return new CodeModel(codebase, graph);
    }

    private static MetricsResult DummyMetricsResult()
    {
        var summary = new SummaryMetrics(
            TotalNamespaces: 0,
            TotalTypes: 0,
            MeanTypesPerNamespace: 0,
            TotalSloc: 0,
            AvgSlocPerType: 0,
            MedianSlocPerType: 0,
            StdDevSlocPerType: 0,
            TotalMethods: 0,
            AvgMethodsPerType: 0,
            MedianMethodsPerType: 0,
            StdDevMethodsPerType: 0,
            TotalComplexity: 0,
            AvgComplexityPerType: 0,
            MedianComplexityPerType: 0,
            StdDevComplexityPerType: 0
        );

        return new MetricsResult(
            Summary: summary,
            Namespaces: new List<NamespaceMetrics>(),
            Types: new List<TypeMetrics>(),
            Methods: new List<MethodMetrics>(),
            NamespaceCoupling: new List<NamespaceCouplingMetrics>(),
            TypeCoupling: new List<TypeCouplingMetrics>(),
            Dependencies: new DependencyMetrics(0, 0, new List<DependencyCycle>())
        );
    }

    #endregion
}
