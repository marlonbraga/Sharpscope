namespace Sharpscope.Domain.Models;

/// <summary>
/// Basic namespace metrics: NOC and NAC.
/// </summary>
public sealed record NamespaceMetrics(
    string Namespace,
    int Noc,   // Number of Types/Classes
    int Nac    // Number of Abstract Classes
);
