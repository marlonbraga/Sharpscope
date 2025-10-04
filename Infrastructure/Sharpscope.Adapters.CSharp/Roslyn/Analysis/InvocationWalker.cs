using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Counts invocation expressions inside a node (e.g., a method body).
/// Includes instance/static/extension method calls. Does not count object creation.
/// Fluent chains like a.B().C() count as 2 invocations.
/// </summary>
public sealed class InvocationWalker : CSharpSyntaxWalker
{
    #region State

    private int _count;

    #endregion

    #region Public API

    /// <summary>
    /// Returns the number of <see cref="InvocationExpressionSyntax"/> inside <paramref name="node"/>.
    /// </summary>
    public static int Compute(SyntaxNode node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        var w = new InvocationWalker();
        w.Visit(node);
        return w._count;
    }

    #endregion

    #region Visits

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        _count++;
        base.VisitInvocationExpression(node);
    }

    #endregion
}
