using Microsoft.CodeAnalysis;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Infrastructure.Sources;
using Shouldly;


namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Workspace;

public sealed class RoslynWorkspaceLoaderTests
{
    [Fact(DisplayName = "LoadCompilationAsync from directory (fallback) parses all .cs respecting filters")]
    public async Task Load_FromDirectory_Works()
    {
        // Arrange: temp dir with 2 .cs and one excluded under bin/
        var root = CreateTempDir();
        var aPath = Path.Combine(root, "A.cs");
        var bPath = Path.Combine(root, "B.cs");
        var excl = Path.Combine(root, "bin", "Debug", "X.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(excl)!);

        await File.WriteAllTextAsync(aPath, "namespace N { public class A { } }");
        await File.WriteAllTextAsync(bPath, "namespace N { public class B { } }");
        await File.WriteAllTextAsync(excl, "namespace N { public class X { } }");

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false, filters: PathFilters.Default());

        // Act
        var comp = await loader.LoadCompilationAsync(root, CancellationToken.None);

        // Assert
        comp.ShouldNotBeNull();
        comp.AssemblyName.ShouldBe("Sharpscope.FromDirectory");
        comp.SyntaxTrees.ShouldNotBeEmpty();
        comp.SyntaxTrees.ShouldContain(t => t.FilePath == aPath);
        comp.SyntaxTrees.ShouldContain(t => t.FilePath == bPath);
        comp.SyntaxTrees.ShouldNotContain(t => t.FilePath == excl);
    }

    [Fact(DisplayName = "LoadCompilationAsync ignores MSBuild when allowMsbuild=false (safe for tests)")]
    public async Task Load_MsbuildDisabled_Fallbacks()
    {
        var root = CreateTempDir();
        var proj = Path.Combine(root, "fake.csproj");
        await File.WriteAllTextAsync(proj, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "C.cs"), "class C{}");

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false);

        var comp = await loader.LoadCompilationAsync(proj, CancellationToken.None);
        comp.ShouldNotBeNull();
        comp.SyntaxTrees.Count().ShouldBe(1);
    }

    #region Helpers

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "ws", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    #endregion
}
