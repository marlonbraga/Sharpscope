using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// ISourceProvider that materializes sources from a Git repository using <see cref="GitCli"/>.
/// Local path materialization is not supported here (use LocalSourceProvider).
/// </summary>
public sealed class GitSourceProvider : IGitSourceProvider
{
    #region Fields & Ctor

    private readonly GitCli _git;

    public GitSourceProvider(GitCli git)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
    }

    #endregion

    #region ISourceProvider

    public Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo path, CancellationToken ct)
        => throw new NotSupportedException("Use LocalSourceProvider for local paths.");

    public async Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
            throw new ArgumentException("Repository URL is required.", nameof(repoUrl));

        var workRoot = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope", "repos"));
        if (!workRoot.Exists) workRoot.Create();

        var dest = new DirectoryInfo(Path.Combine(workRoot.FullName, Guid.NewGuid().ToString("N")));
        // git clone creates the dest folder; ensure parent exists
        if (!dest.Parent!.Exists) dest.Parent!.Create();

        await _git.CloneAsync(repoUrl, dest, ct).ConfigureAwait(false);
        return dest;
    }

    #endregion
}
