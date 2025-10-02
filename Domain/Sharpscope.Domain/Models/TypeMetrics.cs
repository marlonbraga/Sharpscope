namespace Sharpscope.Domain.Models;

/// <summary>
/// Metrics by type: SLOC, NOM, NPM, WMC, DEP, I-DEP, FAN-IN/OUT, NOA, LCOM3.
/// </summary>
public sealed record TypeMetrics(
    string TypeFullName,
    int Sloc,
    int Nom,
    int Npm,
    int Wmc,
    int Dep,
    int IDep,
    int FanIn,
    int FanOut,
    int Noa,
    double Lcom3
);
