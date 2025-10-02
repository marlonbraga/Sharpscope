namespace Sharpscope.Domain.Models;

/// <summary>
/// Root representation of a codebase analyzed by Sharpscope.
/// Contains the set of modules (projects/assemblies) discovered.
/// </summary>
public sealed record Codebase(IReadOnlyList<ModuleNode> Modules)
{
    public static Codebase Empty { get; } = new([]);
}
