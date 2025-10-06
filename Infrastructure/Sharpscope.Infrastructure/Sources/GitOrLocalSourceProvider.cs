using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Facade exposing ISourceProvider, delegating to specialized providers to avoid DI cycles.
/// </summary>
public sealed class GitOrLocalSourceProvider : ISourceProvider
{
    private readonly IGitSourceProvider _git;
    private readonly ILocalSourceProvider _local;

    public GitOrLocalSourceProvider(IGitSourceProvider git, ILocalSourceProvider local)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _local = local ?? throw new ArgumentNullException(nameof(local));
    }

    public Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo sourceRoot, CancellationToken ct)
        => _local.MaterializeFromLocalAsync(sourceRoot, ct);

    public Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct)
        => _git.MaterializeFromGitAsync(repoUrl, ct);
}
