using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Special-purpose provider for materializing sources from a public Git repository.
/// </summary>
public interface IGitSourceProvider
{
    Task<DirectoryInfo> MaterializeFromGitAsync(string repoUrl, CancellationToken ct);
}
