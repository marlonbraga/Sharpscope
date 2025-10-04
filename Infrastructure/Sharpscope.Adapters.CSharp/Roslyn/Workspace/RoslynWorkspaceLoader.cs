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

        path = Path.GetFullPath(path);

        if (_allowMsbuild && (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                              path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                return await LoadWithMsbuildAsync(path, ct).ConfigureAwait(false);
            }
            catch
            {
                // Fall back if MSBuild not available or project cannot be loaded
            }
        }

        var dir = Directory.Exists(path) ? new DirectoryInfo(path)
                                         : new DirectoryInfo(Path.GetDirectoryName(path)!);

        return await LoadFromDirectoryAsync(dir, ct).ConfigureAwait(false);
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

    private static async Task<Compilation> LoadWithMsbuildAsync(string path, CancellationToken ct)
    {
        EnsureMsBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, __) => { /* optionally log diagnostics */ };

        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var solution = await workspace.OpenSolutionAsync(path, cancellationToken: ct).ConfigureAwait(false);
            return await MergeSolutionCompilationsAsync(solution, ct).ConfigureAwait(false);
        }
        else
        {
            var project = await workspace.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);
            var comp = await project.GetCompilationAsync(ct).ConfigureAwait(false)
                      ?? CSharpCompilation.Create(project.Name);
            return comp;
        }
    }

    private static async Task<Compilation> MergeSolutionCompilationsAsync(Solution solution, CancellationToken ct)
    {
        var csharpProjects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp).ToList();
        var trees = new List<SyntaxTree>();
        var refs = new HashSet<MetadataReference>();

        foreach (var p in csharpProjects)
        {
            var comp = await p.GetCompilationAsync(ct).ConfigureAwait(false) as CSharpCompilation;
            if (comp is null) continue;

            trees.AddRange(comp.SyntaxTrees);
            foreach (var r in comp.References) refs.Add(r);
        }

        if (refs.Count == 0)
            AddBaselineReferences(refs);

        return CSharpCompilation.Create("Sharpscope.Merged", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion

    #region Directory path (fallback)

    private async Task<Compilation> LoadFromDirectoryAsync(DirectoryInfo dir, CancellationToken ct)
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

        return CSharpCompilation.Create("Sharpscope.FromDirectory", trees, refs,
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
