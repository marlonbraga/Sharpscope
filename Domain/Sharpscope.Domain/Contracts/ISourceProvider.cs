namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Materializes a source tree either from a local directory or from a remote repository
/// into a working directory that adapters can consume.
/// </summary>
public interface ISourceProvider
{
    /// <summary>
    /// Uses an existing local directory as the source root (may copy or validate it).
    /// Returns the directory to be analyzed.
    /// </summary>
    Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo path, CancellationToken ct);

    /// <summary>
    /// Clones or downloads a public repository URL into a temporary working directory.
    /// Returns the directory to be analyzed.
    /// </summary>
    Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct);
}
