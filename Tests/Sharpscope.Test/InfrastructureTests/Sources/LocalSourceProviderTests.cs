using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Sharpscope.Domain.Contracts;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class LocalSourceProviderTests
{
    [Fact(DisplayName = "MaterializeFromLocalAsync copies files and respects default excludes")]
    public async Task MaterializeFromLocalAsync_Copies_WithExcludes()
    {
        // Arrange: create a small temp tree
        var srcRoot = CreateTempDir();

        var srcDir = Directory.CreateDirectory(Path.Combine(srcRoot, "project"));
        var keepFile = Path.Combine(srcDir.FullName, "Program.cs");
        var binFile = Path.Combine(srcDir.FullName, "bin", "Debug", "skip.txt");
        var objFile = Path.Combine(srcDir.FullName, "obj", "skip2.txt");
        var gitFile = Path.Combine(srcDir.FullName, ".git", "config");

        Directory.CreateDirectory(Path.GetDirectoryName(keepFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(binFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(objFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(gitFile)!);

        await File.WriteAllTextAsync(keepFile, "// ok");
        await File.WriteAllTextAsync(binFile, "bin");
        await File.WriteAllTextAsync(objFile, "obj");
        await File.WriteAllTextAsync(gitFile, "git");

        var provider = new LocalSourceProvider(PathFilters.Default());

        // Act
        var dst = await provider.MaterializeFromLocalAsync(srcDir, CancellationToken.None);

        // Assert
        var copiedKeep = Path.Combine(dst.FullName, "Program.cs");
        File.Exists(copiedKeep).ShouldBeTrue();

        File.Exists(Path.Combine(dst.FullName, "bin", "Debug", "skip.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(dst.FullName, "obj", "skip2.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(dst.FullName, ".git", "config")).ShouldBeFalse();
    }

    [Fact(DisplayName = "MaterializeFromGitAsync is not supported in LocalSourceProvider")]
    public async Task MaterializeFromGitAsync_NotSupported()
    {
        var provider = new LocalSourceProvider();
        await Should.ThrowAsync<NotSupportedException>(() =>
            provider.MaterializeFromGitAsync("https://example/repo.git", CancellationToken.None));
    }

    [Fact(DisplayName = "MaterializeFromLocalAsync throws for non-existent directory")]
    public async Task MaterializeFromLocalAsync_Throws_WhenDirectoryMissing()
    {
        var provider = new LocalSourceProvider();
        var missing = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", Guid.NewGuid().ToString("N")));

        await Should.ThrowAsync<DirectoryNotFoundException>(() =>
            provider.MaterializeFromLocalAsync(missing, CancellationToken.None));
    }

    #region Helpers

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    #endregion
}
