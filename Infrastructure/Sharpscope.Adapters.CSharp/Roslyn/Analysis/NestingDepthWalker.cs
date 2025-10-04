using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

public sealed class NestingDepthWalker : CSharpSyntaxWalker
{
    private int _current;
    private int _max;

    public static int Compute(SyntaxNode node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        var w = new NestingDepthWalker();
        w.Visit(node);
        return w._max;
    }

    private void Enter(Action visit)
    {
        _current++;
        if (_current > _max) _max = _current;
        visit();
        _current--;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        Enter(() =>
        {
            Visit(node.Condition);
            Visit(node.Statement);
            if (node.Else is not null) Visit(node.Else);
        });
    }

    public override void VisitElseClause(ElseClauseSyntax node)
    {
        if (node.Statement is IfStatementSyntax chainedIf) // else-if
        {
            // Count the 'else' as one nesting level, but do NOT add another level for the chained 'if'.
            Enter(() =>
            {
                // Manually traverse the chained-if WITHOUT triggering another Enter from VisitIfStatement:
                Visit(chainedIf.Condition);
                Visit(chainedIf.Statement);
                if (chainedIf.Else is not null)
                    Visit(chainedIf.Else);
            });
        }
        else
        {
            // Regular else: increases depth once
            Enter(() => base.VisitElseClause(node));
        }
    }
    public override void VisitWhileStatement(WhileStatementSyntax node) => Enter(() => base.VisitWhileStatement(node));
    public override void VisitDoStatement(DoStatementSyntax node) => Enter(() => base.VisitDoStatement(node));
    public override void VisitForStatement(ForStatementSyntax node) => Enter(() => base.VisitForStatement(node));
    public override void VisitForEachStatement(ForEachStatementSyntax node) => Enter(() => base.VisitForEachStatement(node));
    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) => Enter(() => base.VisitForEachVariableStatement(node));
    public override void VisitUsingStatement(UsingStatementSyntax node) => Enter(() => base.VisitUsingStatement(node));
    public override void VisitLockStatement(LockStatementSyntax node) => Enter(() => base.VisitLockStatement(node));
    public override void VisitFixedStatement(FixedStatementSyntax node) => Enter(() => base.VisitFixedStatement(node));
    public override void VisitCheckedStatement(CheckedStatementSyntax node) => Enter(() => base.VisitCheckedStatement(node));
    public override void VisitTryStatement(TryStatementSyntax node) => Enter(() => base.VisitTryStatement(node));
    public override void VisitCatchClause(CatchClauseSyntax node) => Enter(() => base.VisitCatchClause(node));
    public override void VisitFinallyClause(FinallyClauseSyntax node) => Enter(() => base.VisitFinallyClause(node));
    public override void VisitSwitchStatement(SwitchStatementSyntax node) => Enter(() => base.VisitSwitchStatement(node));
}
