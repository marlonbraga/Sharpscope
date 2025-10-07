using Sharpscope.Adapters.CSharp;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Sources; // PathFilters
using Shouldly;

namespace Sharpscope.Test.AdapterCSharpTests
{
    public sealed class CSharpLanguageAdapterTests
    {
        [Fact(DisplayName = "BuildModelAsync handles partial class across files (cross-tree bodies)")]
        public async Task BuildModelAsync_PartialAcrossFiles_DoesNotThrow_AndBuildsType()
        {
            var root = CreateTempDir();

            var code1 = @"
namespace N {
    public partial class P
    {
        private int _x;
        public P() { _x = 1; }
    }
}";
            var code2 = @"
namespace N {
    public partial class P
    {
        public void M() { _x++; System.Console.WriteLine(_x); }
    }
}";
            await File.WriteAllTextAsync(Path.Combine(root, "P1.cs"), code1);
            await File.WriteAllTextAsync(Path.Combine(root, "P2.cs"), code2);

            var filters = PathFilters.Default();
            var loader = new RoslynWorkspaceLoader(allowMsbuild: false, filters);
            var builder = new CSharpModelBuilder();
            var adapter = new CSharpLanguageAdapter(loader, builder);

            CodeModel model = null!;
            var ex = await Record.ExceptionAsync(() => adapter.BuildModelAsync(new DirectoryInfo(root), CancellationToken.None));
            ex.ShouldBeNull();

            model = await adapter.BuildModelAsync(new DirectoryInfo(root), CancellationToken.None);

            var p = model.Codebase.Modules.SelectMany(m => m.Namespaces)
                                          .SelectMany(n => n.Types)
                                          .FirstOrDefault(t => t.FullName == "N.P");
            p.ShouldNotBeNull();
            p!.Methods.Count.ShouldBe(2); // ctor + M
        }

        [Fact(DisplayName = "BuildModelAsync handles multiple trees with method bodies in both")]
        public async Task BuildModelAsync_MultiTreesWithBodies_DoesNotThrow()
        {
            var root = CreateTempDir();

            var codeA = @"
namespace N {
    public class A
    {
        public void MA() { var b = new B(); System.Console.WriteLine(b.ToString()); }
    }
}";
            var codeB = @"
namespace N {
    public class B
    {
        public void MB() { System.Console.WriteLine(123); }
    }
}";
            await File.WriteAllTextAsync(Path.Combine(root, "A.cs"), codeA);
            await File.WriteAllTextAsync(Path.Combine(root, "B.cs"), codeB);

            var filters = PathFilters.Default();
            var loader = new RoslynWorkspaceLoader(allowMsbuild: false, filters);
            var builder = new CSharpModelBuilder();
            var adapter = new CSharpLanguageAdapter(loader, builder);

            var ex = await Record.ExceptionAsync(() => adapter.BuildModelAsync(new DirectoryInfo(root), CancellationToken.None));
            ex.ShouldBeNull();
        }

        #region helpers

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "adapter", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        #endregion
    }
}
