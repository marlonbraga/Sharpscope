using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Collects names of fields from the current type that are accessed within a node (e.g., a method).
/// Requires a <see cref="SemanticModel"/> to resolve symbols and ignore locals/parameters/properties.
/// Static and instance fields are included; fields from other types are ignored.
/// </summary>
public sealed class FieldAccessWalker : CSharpSyntaxWalker
{
    #region State

    private readonly SemanticModel _model;
    private readonly ITypeSymbol _owningType;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    #endregion

    #region Ctor

    private FieldAccessWalker(SemanticModel model, ITypeSymbol owningType)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _owningType = owningType ?? throw new ArgumentNullException(nameof(owningType));
    }

    #endregion

    #region Public API

    /// <summary>
    /// Returns the distinct field names of the <b>owning type</b> accessed within <paramref name="node"/>.
    /// If <paramref name="owningType"/> is null, it is inferred from the nearest <see cref="TypeDeclarationSyntax"/>.
    /// </summary>
    public static IReadOnlyCollection<string> Compute(SyntaxNode node, SemanticModel model, INamedTypeSymbol? owningType = null)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (model is null) throw new ArgumentNullException(nameof(model));

        var typeSym = owningType as ITypeSymbol ?? InferOwningType(node, model)
            ?? throw new InvalidOperationException("Could not infer owning type from syntax node.");

        var w = new FieldAccessWalker(model, typeSym);
        w.Visit(node);
        return w._names;
    }

    #endregion

    #region Visits

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        TryAddField(node);
        base.VisitIdentifierName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // e.g., this.F, C.S, obj.F
        TryAddField(node.Name);
        base.VisitMemberAccessExpression(node);
    }

    #endregion

    #region Helpers

    private static INamedTypeSymbol? InferOwningType(SyntaxNode node, SemanticModel model)
    {
        var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        return typeDecl is null ? null : model.GetDeclaredSymbol(typeDecl);
    }

    private void TryAddField(SyntaxNode nameNode)
    {
        var symbol = _model.GetSymbolInfo(nameNode).Symbol;
        if (symbol is IFieldSymbol field &&
            SymbolEqualityComparer.Default.Equals(field.ContainingType, _owningType))
        {
            _names.Add(field.Name);
        }
    }

    #endregion
}
