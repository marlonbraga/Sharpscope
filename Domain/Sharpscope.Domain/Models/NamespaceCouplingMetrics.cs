namespace Sharpscope.Domain.Models;

/// <summary>
/// Coupling by namespace: CA, CE, I, A, D.
/// </summary>
public sealed record NamespaceCouplingMetrics(
    string Namespace,
    int Ca,
    int Ce,
    double Instability,        // I = CE / (CA + CE)
    double Abstractness,       // A = tipos abstratos / tipos
    double NormalizedDistance  // D = |A + I - 1|
);
