namespace Sharpscope.Domain.Models;

/// <summary>
/// Field/attribute of a type.
/// </summary>
public sealed record FieldNode(
    string Name,
    string TypeName,
    bool IsPublic
);
