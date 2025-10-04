using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// ISourceProvider that materializes sources from a local directory into a temp working directory,
/// applying include/exclude filters.
/// </summary>
public sealed class LocalSourceProvider : ISourceProvider
{
    #region Fields & Ctor

    private readonly PathFilters _filters;

    public LocalSourceProvider(PathFilters? filters = null)
    {
        _filters = filters ?? PathFilters.Default();
    }

    #endregion

    #region ISourceProvider

    public async Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo path, CancellationToken ct)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (!path.Exists) throw new DirectoryNotFoundException($"Directory not found: {path.FullName}");

        var dest = CreateWorkDirectory();

        // Copy is I/O bound; use Task.Run to avoid blocking caller thread in sync loops.
        await Task.Run(() => CopyTree(path, dest, ct), ct).ConfigureAwait(false);

        return dest;
    }

    public Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct)
        => throw new NotSupportedException("Use GitSourceProvider for Git repositories.");

    #endregion

    #region Helpers

    private static DirectoryInfo CreateWorkDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpscope", "work", Guid.NewGuid().ToString("N"));
        var di = new DirectoryInfo(root);
        di.Create();
        return di;
    }

    private void CopyTree(DirectoryInfo src, DirectoryInfo dst, CancellationToken ct)
    {
        var srcRoot = src.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var rel = Path.GetRelativePath(srcRoot, file);
            var relNorm = PathFilters.NormalizePath(rel);

            if (!_filters.ShouldInclude(relNorm))
                continue;

            var destFile = Path.Combine(dst.FullName, rel);
            var destDir = Path.GetDirectoryName(destFile)!;
            Directory.CreateDirectory(destDir);

            File.Copy(file, destFile, overwrite: true);
        }
    }

    #endregion
}
