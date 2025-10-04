using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
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

    [Fact(DisplayName = "ExecuteAsync (local path) runs full pipeline and writes via selected writer")]
    public async Task ExecuteAsync_Local_WritesReport()
    {
        // Arrange
        var work = CreateTempDir();
        var output = Path.Combine(work.FullName, "out.json");

        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(work));

        var detector = Substitute.For<ILanguageDetector>();
        detector.DetectLanguageAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("csharp"));

        var adapter = Substitute.For<ILanguageAdapter>();
        adapter.LanguageId.Returns("csharp");
        adapter.CanHandle("csharp").Returns(true);
        adapter.BuildModelAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(CreateMinimalModel()));

        var engine = Substitute.For<IMetricsEngine>();
        var metrics = (MetricsResult)FormatterServices.GetUninitializedObject(typeof(MetricsResult));
        engine.Compute(Arg.Any<CodeModel>()).Returns(metrics);

        var jsonWriter = Substitute.For<IReportWriter>();
        jsonWriter.Format.Returns("json");
        jsonWriter.WriteAsync(metrics, Arg.Any<FileInfo>(), Arg.Any<CancellationToken>())
                  .Returns(Task.CompletedTask);

        var sut = new AnalyzeSolutionUseCase(
            source,
            detector,
            new[] { adapter },
            engine,
            new[] { jsonWriter }
        );

        var req = new AnalyzeRequest(
            Path: work.FullName,
            RepoUrl: null,
            Format: "json",
            OutputPath: output
        );

        // Act
        var file = await sut.ExecuteAsync(req, CancellationToken.None);

        // Assert
        file.FullName.ShouldBe(output);

        await source.Received(1).MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>());
        await detector.Received(1).DetectLanguageAsync(work, Arg.Any<CancellationToken>());
        await adapter.Received(1).BuildModelAsync(work, Arg.Any<CancellationToken>());
        engine.Received(1).Compute(Arg.Any<CodeModel>());

        await jsonWriter.Received(1).WriteAsync(
            metrics,
            Arg.Is<FileInfo>(f => f.FullName == output),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Happy-path: repo URL

    [Fact(DisplayName = "ExecuteAsync (repo url) materializes via git")]
    public async Task ExecuteAsync_Repo_WritesReport()
    {
        var work = CreateTempDir();
        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(work));

        var detector = StubDetector("csharp");
        var adapter = StubAdapter("csharp");
        var engine = StubEngine();
        var writer = StubWriter("md");

        var sut = new AnalyzeSolutionUseCase(
            source, detector,
            new[] { adapter },
            engine,
            new[] { writer });

        var req = new AnalyzeRequest(
            Path: null,
            RepoUrl: "https://example/repo.git",
            Format: "md",
            OutputPath: null // default under workdir
        );

        var outFile = await sut.ExecuteAsync(req, CancellationToken.None);

        await source.Received(1).MaterializeFromGitAsync("https://example/repo.git", Arg.Any<CancellationToken>());
        outFile.Name.ShouldBe("sharpscope-report.md");
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
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());

        var req = new AnalyzeRequest(work.FullName, null, "json", null);

        await Should.ThrowAsync<NotSupportedException>(() => sut.ExecuteAsync(req, CancellationToken.None));
    }

    [Fact(DisplayName = "Throws when unknown report format")]
    public async Task ExecuteAsync_UnknownFormat_Throws()
    {
        var work = CreateTempDir();

        var sut = new AnalyzeSolutionUseCase(
            StubSource(work),
            StubDetector("csharp"),
            new[] { StubAdapter("csharp") },
            StubEngine(),
            Array.Empty<IReportWriter>());

        var req = new AnalyzeRequest(work.FullName, null, "pdf", null);

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

    private static CodeModel CreateMinimalModel()
    {
        var module = new ModuleNode("M", new List<NamespaceNode> { new NamespaceNode("N", new List<TypeNode>()) });
        var codebase = new Codebase(new List<ModuleNode> { module });
        var graph = new DependencyGraph(
            new Dictionary<string, IReadOnlyCollection<string>>(),
            new Dictionary<string, IReadOnlyCollection<string>>());
        return new CodeModel(codebase, graph);
    }

    private static ISourceProvider StubSource(DirectoryInfo work)
    {
        var s = Substitute.For<ISourceProvider>();
        s.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(work));
        return s;
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
        a.BuildModelAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(CreateMinimalModel()));
        return a;
    }

    private static IMetricsEngine StubEngine()
    {
        var e = Substitute.For<IMetricsEngine>();
        var mr = (MetricsResult)FormatterServices.GetUninitializedObject(typeof(MetricsResult));
        e.Compute(Arg.Any<CodeModel>()).Returns(mr);
        return e;
    }

    private static IReportWriter StubWriter(string format)
    {
        var w = Substitute.For<IReportWriter>();
        w.Format.Returns(format);
        w.WriteAsync(Arg.Any<MetricsResult>(), Arg.Any<FileInfo>(), Arg.Any<CancellationToken>())
         .Returns(Task.CompletedTask);
        return w;
    }

    private AnalyzeSolutionUseCase NewSut()
    {
        return new AnalyzeSolutionUseCase(
            Substitute.For<ISourceProvider>(),
            Substitute.For<ILanguageDetector>(),
            Array.Empty<ILanguageAdapter>(),
            Substitute.For<IMetricsEngine>(),
            Array.Empty<IReportWriter>());
    }

    #endregion
}
