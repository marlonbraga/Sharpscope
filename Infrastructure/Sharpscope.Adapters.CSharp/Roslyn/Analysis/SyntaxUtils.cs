using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Syntax helpers independent from project state or symbols.
/// </summary>
public static class SyntaxUtils
{
    #region Namespace discovery

    /// <summary>
    /// Returns the fully-qualified namespace (dotted) that encloses the given type declaration.
    /// Supports both block and file-scoped namespaces. Returns empty string if in global namespace.
    /// </summary>
    public static string GetDeclaredNamespace(TypeDeclarationSyntax typeDecl)
    {
        if (typeDecl is null) throw new ArgumentNullException(nameof(typeDecl));
        var names = new Stack<string>();

        // Walk parents collecting namespaces
        for (SyntaxNode? n = typeDecl.Parent; n is not null; n = n.Parent)
        {
            switch (n)
            {
                case NamespaceDeclarationSyntax ns:
                    names.Push(ns.Name.ToString());
                    break;
                case FileScopedNamespaceDeclarationSyntax fns:
                    names.Push(fns.Name.ToString());
                    // file-scoped is the outermost; keep walking upward for nested file-scoped (rare).
                    break;
            }
        }

        return string.Join(".", names);
    }

    #endregion

    #region Type enumeration

    /// <summary>
    /// Enumerates all type declarations (classes/structs/interfaces/records) in a syntax node.
    /// </summary>
    public static IEnumerable<TypeDeclarationSyntax> EnumerateTypes(SyntaxNode root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        foreach (var t in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            yield return t;
    }

    #endregion
}
