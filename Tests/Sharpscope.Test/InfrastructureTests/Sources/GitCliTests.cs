using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class GitCliTests
{
    [Fact(DisplayName = "CloneAsync passes shallow args and destination to git")]
    public async Task CloneAsync_ShallowArgs_ArePassed()
    {
        // Arrange
        var runner = Substitute.For<IProcessRunner>();
        var git = new GitCli(runner);
        var dest = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", Guid.NewGuid().ToString("N")));
        dest.Parent!.Create(); // ensure parent exists

        runner.RunAsync(
            "git",
            Arg.Is<string>(a => a.Contains("clone") && a.Contains("--depth 1") && a.Contains($"\"{dest.FullName}\"")),
            Arg.Any<DirectoryInfo>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new ProcessResult(0, "ok", "")));

        // Act
        await git.CloneAsync("https://example/repo.git", dest, CancellationToken.None);

        // Assert (received call validated by Arg.Is)
        await runner.Received(1).RunAsync(
            "git",
            Arg.Any<string>(),
            Arg.Any<DirectoryInfo>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CloneAsync includes branch args when provided")]
    public async Task CloneAsync_WithBranch_IncludesArgs()
    {
        // Arrange
        var runner = Substitute.For<IProcessRunner>();
        var git = new GitCli(runner);
        var dest = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", Guid.NewGuid().ToString("N")));
        dest.Parent!.Create();

        runner.RunAsync(
            "git",
            Arg.Is<string>(a => a.Contains("-b main") && a.Contains("--single-branch")),
            Arg.Any<DirectoryInfo>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new ProcessResult(0, "ok", "")));

        // Act
        await git.CloneAsync("https://example/repo.git", dest, CancellationToken.None, branch: "main");

        // Assert
        await runner.Received(1).RunAsync(
            "git",
            Arg.Any<string>(),
            Arg.Any<DirectoryInfo>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CloneAsync throws when git returns non-zero exit code")]
    public async Task CloneAsync_OnFailure_Throws()
    {
        // Arrange
        var runner = Substitute.For<IProcessRunner>();
        var git = new GitCli(runner);
        var dest = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", Guid.NewGuid().ToString("N")));
        dest.Parent!.Create();

        runner.RunAsync(
            "git",
            Arg.Any<string>(),
            Arg.Any<DirectoryInfo>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new ProcessResult(128, "", "fatal: repository not found")));

        // Act + Assert
        await Should.ThrowAsync<InvalidOperationException>(() =>
            git.CloneAsync("https://example/does-not-exist.git", dest, CancellationToken.None));
    }
}
