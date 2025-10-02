using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Method representation in agnostic IR.
/// </summary>
public sealed record MethodNode(
    string FullName,
    int Parameters,
    int Sloc,
    int DecisionPoints,          // para CYCLO = 1 + DecisionPoints
    int MaxNestingDepth,         // NBD
    int Calls,                   // invocações
    bool IsPublic,
    IReadOnlyList<string> AccessedFields
);
