using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Computes Cyclomatic Complexity = 1 + (decision points).
/// Decision points included:
/// - if / else-if
/// - switch (sum of non-default case labels)
/// - switch expressions (number of arms)
/// - loops: for / foreach / while / do
/// - catch clauses
/// - conditional operator ?:
/// - boolean operators && and ||
/// - null-coalescing operator ??
/// Notes:
/// - 'else-if' is another IfStatement and is counted.
/// - Default case label is ignored for switch statements.
/// </summary>
public sealed class CyclomaticComplexityWalker : CSharpSyntaxWalker
{
    #region State

    private int _decisions;

    #endregion

    #region Public API

    /// <summary>
    /// Computes cyclomatic complexity for any syntax node (e.g., MethodDeclarationSyntax).
    /// </summary>
    public static int Compute(SyntaxNode node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        var w = new CyclomaticComplexityWalker();
        w.Visit(node);
        return 1 + w._decisions;
    }

    #endregion

    #region Visits (statements)

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        _decisions++; // each 'if' or 'else if'
        base.VisitIfStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        _decisions++;
        base.VisitWhileStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        _decisions++;
        base.VisitDoStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        _decisions++;
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        _decisions++;
        base.VisitForEachStatement(node);
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        _decisions++;
        base.VisitForEachVariableStatement(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        _decisions++;
        base.VisitCatchClause(node);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        // Count non-default case labels
        foreach (var section in node.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax) _decisions++;
            }
        }
        base.VisitSwitchStatement(node);
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        // Count all arms
        _decisions += node.Arms.Count;
        base.VisitSwitchExpression(node);
    }

    #endregion

    #region Visits (expressions)

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        _decisions++; // ?:
        base.VisitConditionalExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        switch (node.Kind())
        {
            case SyntaxKind.LogicalAndExpression:
            case SyntaxKind.LogicalOrExpression:
            case SyntaxKind.CoalesceExpression:
                _decisions++;
                break;
        }
        base.VisitBinaryExpression(node);
    }

    #endregion
}
