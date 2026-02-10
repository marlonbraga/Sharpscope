using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Sources;
using Shouldly;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Modeling;

public sealed class CodeGraphBuilderTests
{
    [Fact(DisplayName = "Build produces full hierarchy and no duplicate nodes")]
    public async Task Build_GraphHasHierarchy_NoDuplicates()
    {
        var root = CreateTempDir();
        var code = @"
namespace N {
    public class A
    {
        public void M() { }
    }
}";
        await File.WriteAllTextAsync(Path.Combine(root, "A.cs"), code);

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false, PathFilters.Default());
        var workspace = await loader.LoadWorkspaceAsync(root, CancellationToken.None);
        var builder = new CodeGraphBuilder();

        var graph = builder.Build(workspace, CancellationToken.None);

        graph.Nodes.Count.ShouldBe(graph.Nodes.Keys.Distinct().Count());

        var solution = graph.Nodes.Values.FirstOrDefault(n => n.Kind == GraphNodeKind.Solution);
        var project = graph.Nodes.Values.FirstOrDefault(n => n.Kind == GraphNodeKind.Project);
        var ns = graph.Nodes.Values.FirstOrDefault(n => n.Kind == GraphNodeKind.Namespace && n.Name == "N");
        var type = graph.Nodes.Values.FirstOrDefault(n => n.Kind == GraphNodeKind.Type && n.Name == "N.A");
        var method = graph.Nodes.Values.FirstOrDefault(n => n.Kind == GraphNodeKind.Method && n.Name.Contains("A.M"));

        solution.ShouldNotBeNull();
        project.ShouldNotBeNull();
        ns.ShouldNotBeNull();
        type.ShouldNotBeNull();
        method.ShouldNotBeNull();

        graph.Edges.ShouldContain(e => e.Kind == GraphEdgeKind.Contains && e.FromId == solution!.Id && e.ToId == project!.Id);
        graph.Edges.ShouldContain(e => e.Kind == GraphEdgeKind.Contains && e.FromId == project!.Id && e.ToId == ns!.Id);
        graph.Edges.ShouldContain(e => e.Kind == GraphEdgeKind.Contains && e.FromId == ns!.Id && e.ToId == type!.Id);
        graph.Edges.ShouldContain(e => e.Kind == GraphEdgeKind.Contains && e.FromId == type!.Id && e.ToId == method!.Id);
    }

    [Fact(DisplayName = "Build produces no duplicate edges")]
    public async Task Build_NoDuplicateEdges()
    {
        var root = CreateTempDir();
        var code = @"
namespace N {
    public class A
    {
        public int P { get; set; }
        public A() { }
        public void M() { }
    }
}";
        await File.WriteAllTextAsync(Path.Combine(root, "A.cs"), code);

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false, PathFilters.Default());
        var workspace = await loader.LoadWorkspaceAsync(root, CancellationToken.None);
        var builder = new CodeGraphBuilder();

        var graph = builder.Build(workspace, CancellationToken.None);

        var unique = graph.Edges
            .Select(e => $"{e.Kind}:{e.FromId}->{e.ToId}")
            .Distinct(StringComparer.Ordinal)
            .Count();

        unique.ShouldBe(graph.Edges.Count);
    }

    [Fact(DisplayName = "Build is deterministic for same input")]
    public async Task Build_DeterministicIds()
    {
        var root = CreateTempDir();
        var code = @"
namespace N {
    public class A
    {
        public void M(int x) { }
    }
}";
        await File.WriteAllTextAsync(Path.Combine(root, "A.cs"), code);

        var loader = new RoslynWorkspaceLoader(allowMsbuild: false, PathFilters.Default());
        var workspace1 = await loader.LoadWorkspaceAsync(root, CancellationToken.None);
        var workspace2 = await loader.LoadWorkspaceAsync(root, CancellationToken.None);
        var builder = new CodeGraphBuilder();

        var g1 = builder.Build(workspace1, CancellationToken.None);
        var g2 = builder.Build(workspace2, CancellationToken.None);

        var nodes1 = g1.Nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var nodes2 = g2.Nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        nodes1.ShouldBe(nodes2);

        var edges1 = g1.Edges.Select(e => $"{e.Kind}:{e.FromId}->{e.ToId}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        var edges2 = g2.Edges.Select(e => $"{e.Kind}:{e.FromId}->{e.ToId}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        edges1.ShouldBe(edges2);
    }

    [Fact(DisplayName = "Build from Sharpscope.sln yields real project ids")]
    public async Task Build_FromSolution_UsesCsprojIds()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var slnPath = Path.Combine(repoRoot, "Sharpscope.sln");
        File.Exists(slnPath).ShouldBeTrue();

        var loader = new RoslynWorkspaceLoader(allowMsbuild: true, PathFilters.Default());
        var workspace = await loader.LoadWorkspaceAsync(slnPath, CancellationToken.None);
        var builder = new CodeGraphBuilder();

        var graph = builder.Build(workspace, CancellationToken.None);
        var projects = graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Project).ToList();

        projects.Count.ShouldBeGreaterThan(1);
        projects.ShouldAllBe(p =>
            p.Id.Contains(".csproj", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(p.Id, "project:.", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "graph", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
