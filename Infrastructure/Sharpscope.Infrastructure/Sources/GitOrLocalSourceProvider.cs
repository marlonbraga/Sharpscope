using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Delegates materialization to a local or git provider, depending on which API you call.
/// This is a thin composition wrapper that keeps responsibilities separated.
/// </summary>
public sealed class GitOrLocalSourceProvider : ISourceProvider
{
    #region Fields & Ctor

    private readonly ISourceProvider _local;
    private readonly ISourceProvider _git;

    public GitOrLocalSourceProvider(ISourceProvider localProvider, ISourceProvider gitProvider)
    {
        _local = localProvider ?? throw new ArgumentNullException(nameof(localProvider));
        _git = gitProvider ?? throw new ArgumentNullException(nameof(gitProvider));
    }

    #endregion

    #region ISourceProvider

    public Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo path, CancellationToken ct)
        => _local.MaterializeFromLocalAsync(path, ct);

    public Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct)
        => _git.MaterializeFromGitAsync(repoUrl, ct);

    #endregion
}
