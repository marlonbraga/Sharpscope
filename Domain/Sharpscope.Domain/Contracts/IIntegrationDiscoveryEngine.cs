using System.IO;
using System.Threading;
using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Discovers external integrations from the canonical graph and source tree.
/// </summary>
public interface IIntegrationDiscoveryEngine
{
    Task<IntegrationsSnapshot> DiscoverAsync(CodeGraph graph, DirectoryInfo root, CancellationToken ct);
}
