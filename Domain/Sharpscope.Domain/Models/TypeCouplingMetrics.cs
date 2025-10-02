namespace Sharpscope.Domain.Models;

/// <summary>
/// Coupling by type: DEP, I-DEP, FAN-IN, FAN-OUT.
/// </summary>
public sealed record TypeCouplingMetrics(
    string TypeFullName,
    int Dependencies,          // DEP
    int InternalDependencies,  // I-DEP
    int FanIn,
    int FanOut
);
