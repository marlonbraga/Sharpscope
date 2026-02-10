using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Sources;
using Shouldly;

namespace Sharpscope.Test.DomainTests;

public sealed class MethodCountConsistencyTests
{
    [Fact(DisplayName = "Method counts match between graph and metrics")]
    public async Task MethodCounts_AreConsistent()
    {
        var root = CreateTempDir();
        var code = @"
namespace N {
    public interface IFoo
    {
        void M();
        int P { get; set; }
    }

    public class C : IFoo
    {
        public int P { get; set; }
        public event System.Action E { add { } remove { } }

        public C() { }

        public void M() { }
        void IFoo.M() { }

        public static C operator +(C a, C b) => a;
    }
}";
        await File.WriteAllTextAsync(Path.Combine(root, "C.cs"), code);

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false, PathFilters.Default());
        var workspace = await loader.LoadWorkspaceAsync(root, CancellationToken.None);
        var builder = new CodeGraphBuilder();
        var graph = builder.Build(workspace, CancellationToken.None);

        var metrics = new MetricsEngine().Compute(graph);

        var methodNodes = graph.Nodes.Values.Count(n => n.Kind == GraphNodeKind.Method);
        var metricsMethods = metrics.Methods.Count;
        var summaryMethods = metrics.Summary.TotalMethods;
        var typesMethods = metrics.Types.Values.Sum(t => t.Nom);

        methodNodes.ShouldBe(metricsMethods);
        methodNodes.ShouldBe(summaryMethods);
        methodNodes.ShouldBe(typesMethods);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "method-counts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
