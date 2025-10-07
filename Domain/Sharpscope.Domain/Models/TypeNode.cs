using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Declared type (class/struct/interface/enum/record) in IR.
/// </summary>
public sealed record TypeNode(
    string FullName,
    TypeKind Kind,
    bool IsAbstract,
    IReadOnlyList<FieldNode> Fields,
    IReadOnlyList<MethodNode> Methods,
    IReadOnlyList<string> DependsOnTypes
);

/// <summary>
/// Type Kinds supported in IR.
/// </summary>
public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Record,
    Delegate
}
