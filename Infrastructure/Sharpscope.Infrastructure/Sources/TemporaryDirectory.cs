using System;
using System.IO;
using System.Threading.Tasks;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Creates a temporary directory under a parent (defaults to %TEMP%/sharpscope/work)
/// and deletes it on dispose unless <see cref="KeepOnDispose"/> is true.
/// </summary>
public sealed class TemporaryDirectory : IDisposable, IAsyncDisposable
{
    #region Public API

    /// <summary>The created directory.</summary>
    public DirectoryInfo Root { get; }

    /// <summary>If true, the directory will be preserved on dispose.</summary>
    public bool KeepOnDispose { get; }

    /// <summary>
    /// Factory that creates and returns a new temporary directory.
    /// </summary>
    public static TemporaryDirectory Create(string? prefix = "sharpscope", DirectoryInfo? parent = null, bool keepOnDispose = false)
    {
        var parentDir = parent ?? new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope", "work"));
        if (!parentDir.Exists) parentDir.Create();

        var name = $"{(string.IsNullOrWhiteSpace(prefix) ? "tmp" : prefix)}-{Guid.NewGuid():N}";
        var dir = new DirectoryInfo(Path.Combine(parentDir.FullName, name));
        dir.Create();
        return new TemporaryDirectory(dir, keepOnDispose);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DeleteIfNeeded();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        DeleteIfNeeded();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>Returns the absolute path of the directory.</summary>
    public override string ToString() => Root.FullName;

    #endregion

    #region Ctor & Helpers

    private TemporaryDirectory(DirectoryInfo root, bool keepOnDispose)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        KeepOnDispose = keepOnDispose;
    }

    private void DeleteIfNeeded()
    {
        if (KeepOnDispose) return;
        try
        {
            if (Root.Exists)
                Root.Delete(recursive: true);
        }
        catch
        {
            // best-effort cleanup; swallow on dispose
        }
    }

    #endregion
}
