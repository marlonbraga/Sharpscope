using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.ApplicationTests.UseCases;

public sealed class AnalyzeSolutionUseCaseTests
{
    #region Happy-path: local path

    [Fact(DisplayName = "ExecuteAsync (local path) runs full pipeline and returns snapshot")]
    public async Task ExecuteAsync_Local_ReturnsSnapshot()
    {
        // Arrange
        var work = CreateTempDir();

        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(work));

        var detector = Substitute.For<ILanguageDetector>();
        detector.DetectLanguageAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("csharp"));

        var adapter = Substitute.For<ILanguageAdapter>();
        adapter.LanguageId.Returns("csharp");
        adapter.CanHandle("csharp").Returns(true);
        var graph = CreateMinimalGraph();
        adapter.BuildGraphAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(graph));

        var engine = Substitute.For<IMetricsEngine>();
        var metrics = MetricsSnapshot.Empty;
        engine.Compute(Arg.Any<CodeGraph>()).Returns(metrics);

        var sut = new AnalyzeSolutionUseCase(
            source,
            detector,
            new[] { adapter },
            engine
        );

        var req = new AnalyzeRequest(
            Path: work.FullName,
            RepoUrl: null,
            Format: "json",
            OutputPath: null
        );

        // Act
        var snapshot = await sut.ExecuteAsync(req, CancellationToken.None);

        // Assert
        snapshot.Graph.ShouldBe(graph);
        snapshot.Metrics.ShouldBe(metrics);
        snapshot.Metadata.RepoUrlOrPath.ShouldBe(work.FullName);

        await source.Received(1).MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>());
        await detector.Received(1).DetectLanguageAsync(work, Arg.Any<CancellationToken>());
        await adapter.Received(1).BuildGraphAsync(work, Arg.Any<CancellationToken>());
        engine.Received(1).Compute(Arg.Any<CodeGraph>());
    }

    #endregion

    #region Happy-path: repo URL

    [Fact(DisplayName = "ExecuteAsync (repo url) materializes via git")]
    public async Task ExecuteAsync_Repo_Works()
    {
        var work = CreateTempDir();
        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(work));

        var detector = StubDetector("csharp");
        var adapter = StubAdapter("csharp");
        var engine = StubEngine();

        var sut = new AnalyzeSolutionUseCase(
            source, detector,
            new[] { adapter },
            engine);

        var req = new AnalyzeRequest(
            Path: null,
            RepoUrl: "https://example/repo.git",
            Format: "md",
            OutputPath: null
        );

        var snapshot = await sut.ExecuteAsync(req, CancellationToken.None);

        await source.Received(1).MaterializeFromGitAsync("https://example/repo.git", Arg.Any<CancellationToken>());
        snapshot.ShouldNotBeNull();
    }

    #endregion

    #region Errors

    [Fact(DisplayName = "Throws when both Path and RepoUrl are provided")]
    public async Task ExecuteAsync_BothInputs_Throws()
    {
        var sut = NewSut();
        var req = new AnalyzeRequest("c:\\x", "https://y", "json", null);

        await Should.ThrowAsync<ArgumentException>(() => sut.ExecuteAsync(req, CancellationToken.None));
    }

    [Fact(DisplayName = "Throws when neither Path nor RepoUrl are provided")]
    public async Task ExecuteAsync_NoInputs_Throws()
    {
        var sut = NewSut();
        var req = new AnalyzeRequest(null, null, "json", null);

        await Should.ThrowAsync<ArgumentException>(() => sut.ExecuteAsync(req, CancellationToken.None));
    }

    [Fact(DisplayName = "Throws when language cannot be detected")]
    public async Task ExecuteAsync_NoLanguage_Throws()
    {
        var work = CreateTempDir();

        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(work));

        var detector = Substitute.For<ILanguageDetector>();
        detector.DetectLanguageAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>(null));

        var sut = new AnalyzeSolutionUseCase(
            source, detector,
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>());

        var req = new AnalyzeRequest(work.FullName, null, "json", null);

        await Should.ThrowAsync<NotSupportedException>(() => sut.ExecuteAsync(req, CancellationToken.None));
    }

    #endregion

    #region Stubs & helpers

    private static DirectoryInfo CreateTempDir()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", "app", Guid.NewGuid().ToString("N")));
        if (!dir.Exists) dir.Create();
        return dir;
    }

    private static CodeGraph CreateMinimalGraph()
    {
        var nodes = new Dictionary<string, GraphNode>();
        var edges = new List<GraphEdge>();
        return new CodeGraph(nodes, edges);
    }

    private static ILanguageDetector StubDetector(string? lang)
    {
        var d = Substitute.For<ILanguageDetector>();
        d.DetectLanguageAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(lang));
        return d;
    }

    private static ILanguageAdapter StubAdapter(string langId)
    {
        var a = Substitute.For<ILanguageAdapter>();
        a.LanguageId.Returns(langId);
        a.CanHandle(langId).Returns(true);
        a.BuildGraphAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(CreateMinimalGraph()));
        return a;
    }

    private static IMetricsEngine StubEngine()
    {
        var e = Substitute.For<IMetricsEngine>();
        e.Compute(Arg.Any<CodeGraph>()).Returns(MetricsSnapshot.Empty);
        return e;
    }

    private AnalyzeSolutionUseCase NewSut()
    {
        return new AnalyzeSolutionUseCase(
            Substitute.For<ISourceProvider>(),
            Substitute.For<ILanguageDetector>(),
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>());
    }

    #endregion
}
