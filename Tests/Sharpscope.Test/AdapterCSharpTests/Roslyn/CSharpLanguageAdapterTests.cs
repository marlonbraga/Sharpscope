using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Adapters.CSharp;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests;

public sealed class CSharpLanguageAdapterTests
{
    [Fact(DisplayName = "CanHandle is case-insensitive for 'csharp'")]
    public void CanHandle_CaseInsensitive()
    {
        var adapter = new CSharpLanguageAdapter(new RoslynWorkspaceLoader(false), new CSharpModelBuilder());
        adapter.CanHandle("csharp").ShouldBeTrue();
        adapter.CanHandle("CSharp").ShouldBeTrue();
        adapter.CanHandle("python").ShouldBeFalse();
    }

    [Fact(DisplayName = "BuildModelAsync builds CodeModel from directory using fallback")]
    public async Task BuildModel_FromDirectory_Works()
    {
        var root = CreateTempDir();
        var code = @"
namespace N {
    public class A {
        public void M() { var b = new B(); }
    }
    public class B { }
}";
        await File.WriteAllTextAsync(Path.Combine(root, "File.cs"), code);

        var adapter = new CSharpLanguageAdapter(new RoslynWorkspaceLoader(allowMsbuild: false), new CSharpModelBuilder());

        var model = await adapter.BuildModelAsync(new DirectoryInfo(root), CancellationToken.None);
        model.ShouldNotBeNull();

        var ns = model.Codebase.Modules.Single().Namespaces.Single(n => n.Name == "N");
        ns.Types.Any(t => t.FullName == "N.A").ShouldBeTrue();
        ns.Types.Any(t => t.FullName == "N.B").ShouldBeTrue();

        model.DependencyGraph.TypeEdges["N.A"].ShouldContain("N.B");
    }

    #region Helpers

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "adapter", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    #endregion
}
