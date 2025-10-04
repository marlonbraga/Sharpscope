using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class GitSourceProviderTests
{
    [Fact(DisplayName = "MaterializeFromGitAsync creates work root and calls git clone for the returned destination")]
    public async Task MaterializeFromGitAsync_CreatesAndClones()
    {
        // Arrange: mock the process runner used by GitCli
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(
                "git",
                Arg.Any<string>(),
                Arg.Any<DirectoryInfo>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(new ProcessResult(0, "ok", "")));

        // Real GitCli (thin wrapper), but using the mocked runner
        var git = new GitCli(runner);

        // SUT
        var provider = new GitSourceProvider(git);

        // Act
        var dir = await provider.MaterializeFromGitAsync("https://example/repo.git", CancellationToken.None);

        // Assert: we got a directory info back
        dir.ShouldNotBeNull();
        dir.Parent.ShouldNotBeNull();

        // Work root (parent) must have been created by provider
        Directory.Exists(dir.Parent!.FullName).ShouldBeTrue();

        // And git must have been invoked pointing to that destination path
        await runner.Received(1).RunAsync(
            "git",
            Arg.Is<string>(a => a.Contains("clone") && a.Contains($"\"{dir.FullName}\"")),
            Arg.Is<DirectoryInfo>(wd => wd.FullName == dir.Parent!.FullName),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MaterializeFromLocalAsync is not supported by GitSourceProvider")]
    public async Task MaterializeFromLocalAsync_NotSupported()
    {
        var provider = new GitSourceProvider(new GitCli(Substitute.For<IProcessRunner>()));
        await Should.ThrowAsync<NotSupportedException>(() =>
            provider.MaterializeFromLocalAsync(new DirectoryInfo(Path.GetTempPath()), CancellationToken.None));
    }
}
