using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Collects field names of the owning type accessed within a syntax node.
/// Safe across multiple trees: every query uses the model for the node's tree.
/// </summary>
public sealed class FieldAccessWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _baselineModel;
    private readonly INamedTypeSymbol _owningType;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    private FieldAccessWalker(SemanticModel baselineModel, INamedTypeSymbol owningType)
        : base(SyntaxWalkerDepth.StructuredTrivia)
    {
        _baselineModel = baselineModel ?? throw new ArgumentNullException(nameof(baselineModel));
        _owningType = owningType ?? throw new ArgumentNullException(nameof(owningType));
    }

    /// <summary>
    /// Main entry: requires the owning type symbol.
    /// </summary>
    public static IReadOnlyCollection<string> Compute(SyntaxNode node, SemanticModel model, INamedTypeSymbol owningType)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (model is null) throw new ArgumentNullException(nameof(model));
        if (owningType is null) throw new ArgumentNullException(nameof(owningType));

        var w = new FieldAccessWalker(model, owningType);
        w.Visit(node);
        return w._names;
    }

    /// <summary>
    /// Convenience overload used by older tests: infers the owning type from the nearest type declaration.
    /// </summary>
    public static IReadOnlyCollection<string> Compute(SyntaxNode node, SemanticModel model)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (model is null) throw new ArgumentNullException(nameof(model));

        var typeDecl = node.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>();
        if (typeDecl is null) return Array.Empty<string>();

        var sm = model.Compilation.GetSemanticModel(typeDecl.SyntaxTree);
        var tsym = sm.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (tsym is null) return Array.Empty<string>();

        return Compute(node, model, tsym);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        TryAddField(node);
        base.VisitIdentifierName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Handles "this._f" or "obj.Field" by resolving the right-side identifier
        TryAddField(node.Name);
        base.VisitMemberAccessExpression(node);
    }

    private void TryAddField(SyntaxNode nameNode)
    {
        // Always use the model bound to this node's tree
        var sm = _baselineModel.Compilation.GetSemanticModel(nameNode.SyntaxTree);
        var sym = sm.GetSymbolInfo(nameNode).Symbol;

        if (sym is IFieldSymbol f && SymbolEqualityComparer.Default.Equals(f.ContainingType, _owningType))
            _names.Add(f.Name);
    }
}
