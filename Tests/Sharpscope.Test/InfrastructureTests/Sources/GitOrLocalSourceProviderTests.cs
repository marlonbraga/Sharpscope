using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Domain.Contracts;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Sources;

public sealed class GitOrLocalSourceProviderTests
{
    [Fact(DisplayName = "MaterializeFromGitAsync delegates to IGitSourceProvider")]
    public async Task MaterializeFromGitAsync_Delegates_To_Git()
    {
        // Arrange
        var git = Substitute.For<IGitSourceProvider>();
        var local = Substitute.For<ILocalSourceProvider>();

        var temp = CreateTempDir();
        git.MaterializeFromGitAsync("https://example/repo.git", Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(temp));

        var sut = new GitOrLocalSourceProvider(git, local);

        // Act
        var dir = await sut.MaterializeFromGitAsync("https://example/repo.git", CancellationToken.None);

        // Assert
        dir.FullName.ShouldBe(temp.FullName);
        await git.Received(1).MaterializeFromGitAsync("https://example/repo.git", Arg.Any<CancellationToken>());
        await local.DidNotReceive().MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MaterializeFromLocalAsync delegates to ILocalSourceProvider")]
    public async Task MaterializeFromLocalAsync_Delegates_To_Local()
    {
        var git = Substitute.For<IGitSourceProvider>();
        var local = Substitute.For<ILocalSourceProvider>();

        var src = CreateTempDir();
        var dst = CreateTempDir();

        local.MaterializeFromLocalAsync(src, Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(dst));

        var sut = new GitOrLocalSourceProvider(git, local);

        var dir = await sut.MaterializeFromLocalAsync(src, CancellationToken.None);

        dir.FullName.ShouldBe(dst.FullName);
        await local.Received(1).MaterializeFromLocalAsync(src, Arg.Any<CancellationToken>());
        await git.DidNotReceive().MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #region helpers

    private static DirectoryInfo CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "infra-src", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new DirectoryInfo(path);
    }

    #endregion
}
