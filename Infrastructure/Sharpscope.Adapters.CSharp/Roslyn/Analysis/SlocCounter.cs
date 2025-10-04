using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Counts SLOC (Source Lines of Code) excluding blank lines, comment-only lines,
/// and lines that contain only closing braces ('}').
/// Preprocessor directives are ignored (do not count as code).
/// Approach: mark lines overlapped by real tokens (trivia such as comments/directives do not count),
/// then filter out empty lines and closing-brace-only lines.
/// </summary>
public static class SlocCounter
{
    #region Public API

    /// <summary>
    /// Counts SLOC over a source string.
    /// </summary>
    public static int Count(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return 0;
        var tree = CSharpSyntaxTree.ParseText(source);
        return Count(tree);
    }

    /// <summary>
    /// Counts SLOC over a syntax tree.
    /// </summary>
    public static int Count(SyntaxTree tree)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));

        var text = tree.GetText();
        if (text.Lines.Count == 0) return 0;

        var root = tree.GetRoot();
        var lineMarks = new bool[text.Lines.Count];

        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            if (token.RawKind == (int)SyntaxKind.EndOfFileToken) continue;

            var span = token.Span;
            var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

            for (int i = startLine; i <= endLine; i++)
                lineMarks[i] = true;
        }

        var count = 0;
        for (int i = 0; i < text.Lines.Count; i++)
        {
            if (!lineMarks[i]) continue;

            var line = text.Lines[i].ToString();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsClosingBraceOnly(line)) continue;

            count++;
        }
        return count;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns true when the line (after trimming whitespace) contains only one or more '}' characters.
    /// </summary>
    private static bool IsClosingBraceOnly(string line)
    {
        var t = line.Trim();
        if (t.Length == 0) return false;

        for (int i = 0; i < t.Length; i++)
        {
            if (t[i] != '}') return false;
        }
        return true;
    }

    #endregion
}
