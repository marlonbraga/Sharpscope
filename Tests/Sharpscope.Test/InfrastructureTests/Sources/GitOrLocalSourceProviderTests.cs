using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Sharpscope.Domain.Contracts;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class GitOrLocalSourceProviderTests
{
    [Fact(DisplayName = "MaterializeFromLocalAsync delegates to local provider")]
    public async Task MaterializeFromLocalAsync_DelegatesToLocal()
    {
        // Arrange
        var local = Substitute.For<ISourceProvider>();
        var git = Substitute.For<ISourceProvider>();

        var expected = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", "local"));
        local.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(expected));

        var provider = new GitOrLocalSourceProvider(local, git);

        // Act
        var result = await provider.MaterializeFromLocalAsync(new DirectoryInfo("C:\\fake"), CancellationToken.None);

        // Assert
        result.FullName.ShouldBe(expected.FullName);
        await local.Received(1).MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>());
        await git.DidNotReceive().MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MaterializeFromGitAsync delegates to git provider")]
    public async Task MaterializeFromGitAsync_DelegatesToGit()
    {
        // Arrange
        var local = Substitute.For<ISourceProvider>();
        var git = Substitute.For<ISourceProvider>();

        var expected = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", "git"));
        git.MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(expected));

        var provider = new GitOrLocalSourceProvider(local, git);

        // Act
        var result = await provider.MaterializeFromGitAsync("https://example/repo.git", CancellationToken.None);

        // Assert
        result.FullName.ShouldBe(expected.FullName);
        await git.Received(1).MaterializeFromGitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await local.DidNotReceive().MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>());
    }
}
