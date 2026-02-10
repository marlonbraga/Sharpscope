using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Sharpscope.Infrastructure.Sources;

namespace Sharpscope.Adapters.CSharp.Roslyn.Workspace;

/// <summary>
/// Loads a Roslyn <see cref="Compilation"/> from a .sln, .csproj or directory.
/// If MSBuild is unavailable or fails, falls back to parsing all .cs files under the directory.
/// </summary>
public sealed class RoslynWorkspaceLoader
{
    #region Fields & Ctor

    private readonly bool _allowMsbuild;
    private readonly PathFilters _filters;

    // One-time MSBuild registration per process
    private static readonly object _msbuildInitLock = new();
    private static bool _msbuildRegistered;

    public RoslynWorkspaceLoader(bool allowMsbuild = true, PathFilters? filters = null)
    {
        _allowMsbuild = allowMsbuild;
        _filters = filters ?? PathFilters.Default();
    }

    #endregion

    #region Public API

    public async Task<Compilation> LoadCompilationAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        var result = await LoadWorkspaceAsync(path, ct).ConfigureAwait(false);
        return MergeCompilations(result.Projects.Select(p => p.Compilation));
    }

    public async Task<RoslynWorkspaceResult> LoadWorkspaceAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        path = Path.GetFullPath(path);

        if (_allowMsbuild && (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                              path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                return await LoadWithMsbuildWorkspaceAsync(path, ct).ConfigureAwait(false);
            }
            catch
            {
                // Fall back if MSBuild not available or project cannot be loaded
            }
        }

        var dir = Directory.Exists(path) ? new DirectoryInfo(path)
                                         : new DirectoryInfo(Path.GetDirectoryName(path)!);

        if (_allowMsbuild && dir.Exists)
        {
            var sln = Directory.EnumerateFiles(dir.FullName, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(sln))
            {
                try
                {
                    return await LoadWithMsbuildWorkspaceAsync(sln, ct).ConfigureAwait(false);
                }
                catch
                {
                    // Fall back if MSBuild not available or project cannot be loaded
                }
            }

            var projects = Directory.EnumerateFiles(dir.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (projects.Count > 0)
            {
                try
                {
                    return await LoadMultipleProjectsAsync(dir.FullName, projects, ct).ConfigureAwait(false);
                }
                catch
                {
                    // Fall back
                }
            }
        }

        return await LoadFromDirectoryWorkspaceAsync(dir, ct).ConfigureAwait(false);
    }

    #endregion

    #region MSBuild path

    private static void EnsureMsBuildRegistered()
    {
        if (_msbuildRegistered) return;
        lock (_msbuildInitLock)
        {
            if (_msbuildRegistered) return;
            try
            {
                // Tries to locate and register MSBuild from VS/.NET SDK installation
                MSBuildLocator.RegisterDefaults();
            }
            catch
            {
                // If registration fails (e.g., machine without MSBuild), we'll fall back to directory parsing.
            }
            _msbuildRegistered = true;
        }
    }

    private static async Task<RoslynWorkspaceResult> LoadWithMsbuildWorkspaceAsync(string path, CancellationToken ct)
    {
        EnsureMsBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, __) => { /* optionally log diagnostics */ };

        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var solution = await workspace.OpenSolutionAsync(path, cancellationToken: ct).ConfigureAwait(false);
            return await BuildResultFromSolutionAsync(solution, path, ct).ConfigureAwait(false);
        }
        else
        {
            var project = await workspace.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);
            var comp = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                      ?? CSharpCompilation.Create(project.Name);

            var root = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
            var item = new ProjectCompilation(project.Name, project.FilePath, comp);
            return new RoslynWorkspaceResult(root, null, new[] { item });
        }
    }

    private static async Task<RoslynWorkspaceResult> BuildResultFromSolutionAsync(
        Solution solution,
        string solutionPath,
        CancellationToken ct)
    {
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        var list = new List<ProjectCompilation>();

        foreach (var p in csharpProjects)
        {
            var comp = await p.GetCompilationAsync(ct).ConfigureAwait(false) as CSharpCompilation;
            if (comp is null) continue;
            list.Add(new ProjectCompilation(p.Name, p.FilePath, comp));
        }

        var root = Directory.Exists(solutionPath)
            ? solutionPath
            : Path.GetDirectoryName(solutionPath)!;

        return new RoslynWorkspaceResult(root, solutionPath, list);
    }

    private static async Task<RoslynWorkspaceResult> LoadMultipleProjectsAsync(
        string rootPath,
        IReadOnlyList<string> projectPaths,
        CancellationToken ct)
    {
        EnsureMsBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, __) => { };

        var list = new List<ProjectCompilation>();
        foreach (var proj in projectPaths)
        {
            ct.ThrowIfCancellationRequested();
            var project = await workspace.OpenProjectAsync(proj, cancellationToken: ct).ConfigureAwait(false);
            if (project.Language != LanguageNames.CSharp) continue;
            var comp = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                      ?? CSharpCompilation.Create(project.Name);
            list.Add(new ProjectCompilation(project.Name, project.FilePath, comp));
        }

        return new RoslynWorkspaceResult(rootPath, null, list);
    }

    #endregion

    #region Directory path (fallback)

    private async Task<RoslynWorkspaceResult> LoadFromDirectoryWorkspaceAsync(DirectoryInfo dir, CancellationToken ct)
    {
        if (dir is null || !dir.Exists)
            throw new DirectoryNotFoundException($"Directory not found: {dir?.FullName}");

        var files = Directory.EnumerateFiles(dir.FullName, "*.cs", SearchOption.AllDirectories)
                             .Where(f => _filters.ShouldInclude(Path.GetRelativePath(dir.FullName, f)))
                             .ToList();

        var trees = new List<SyntaxTree>(files.Count);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            trees.Add(CSharpSyntaxTree.ParseText(text, path: file));
        }

        var refs = new HashSet<MetadataReference>();
        AddBaselineReferences(refs);

        var comp = CSharpCompilation.Create("Sharpscope.FromDirectory", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var project = new ProjectCompilation("Workspace", dir.FullName, comp);
        return new RoslynWorkspaceResult(dir.FullName, null, new[] { project });
    }

    private static Compilation MergeCompilations(IEnumerable<Compilation> comps)
    {
        var trees = new List<SyntaxTree>();
        var refs = new HashSet<MetadataReference>();

        foreach (var comp in comps.OfType<CSharpCompilation>())
        {
            trees.AddRange(comp.SyntaxTrees);
            foreach (var r in comp.References) refs.Add(r);
        }

        if (refs.Count == 0)
            AddBaselineReferences(refs);

        return CSharpCompilation.Create("Sharpscope.Merged", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static void AddBaselineReferences(HashSet<MetadataReference> refs)
    {
        // Provide minimal references so SemanticModel can resolve basic types
        var core = typeof(object).Assembly.Location;
        refs.Add(MetadataReference.CreateFromFile(core));

        var linq = typeof(Enumerable).Assembly.Location;
        refs.Add(MetadataReference.CreateFromFile(linq));

        // Add System.Runtime if available (best-effort)
        var sysRuntime = Path.Combine(Path.GetDirectoryName(core)!, "System.Runtime.dll");
        if (File.Exists(sysRuntime))
            refs.Add(MetadataReference.CreateFromFile(sysRuntime));
    }

    #endregion
}
