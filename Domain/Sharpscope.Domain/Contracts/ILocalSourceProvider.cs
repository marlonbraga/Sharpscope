using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Special-purpose provider for materializing sources from a local directory.
/// </summary>
public interface ILocalSourceProvider
{
    Task<DirectoryInfo> MaterializeFromLocalAsync(DirectoryInfo sourceRoot, CancellationToken ct);
}
