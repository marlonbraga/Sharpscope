namespace Sharpscope.Domain.Models;

/// <summary>
/// Metrics calculated by method (MLOC, CYCLO, CALLS, NBD, PARAM).
/// </summary>
public sealed record MethodMetrics(
    string MethodFullName,
    int Mloc,
    int Cyclo,
    int Calls,
    int Nbd,
    int Parameters
);
