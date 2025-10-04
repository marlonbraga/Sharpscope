using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Sharpscope.Domain.Models;
using DomainTypeKind = Sharpscope.Domain.Models.TypeKind;

namespace Sharpscope.Adapters.CSharp.Roslyn.Analysis;

/// <summary>
/// Symbol helpers that do not depend on syntax form.
/// </summary>
public static class SymbolExtensions
{
    #region Full names

    /// <summary>
    /// Returns a dotted fully-qualified name (without "global::"), including containing types if any.
    /// Examples: "My.App.C", "My.App.Outer+Inner" becomes "My.App.Outer.Inner".
    /// </summary>
    public static string GetFullName(this ISymbol symbol)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));

        // Build from inside out (type nesting), then prepend namespace
        var sb = new StringBuilder();

        // Collect type chain
        var cur = symbol;
        var lastWasType = false;
        while (cur is ITypeSymbol || cur is IMethodSymbol || cur is IFieldSymbol || cur is IPropertySymbol)
        {
            if (cur is IMethodSymbol ms)
            {
                cur = ms.ContainingType ?? ms.ContainingSymbol;
                continue;
            }
            if (cur is IFieldSymbol fs)
            {
                cur = fs.ContainingType ?? fs.ContainingSymbol;
                continue;
            }
            if (cur is IPropertySymbol ps)
            {
                cur = ps.ContainingType ?? ps.ContainingSymbol;
                continue;
            }

            if (cur is ITypeSymbol ts)
            {
                if (sb.Length > 0) sb.Insert(0, ".");
                sb.Insert(0, ts.Name);
                lastWasType = true;
                cur = ts.ContainingType ?? ts.ContainingNamespace as ISymbol;
            }
        }

        // Namespace
        if (cur is INamespaceSymbol ns && !ns.IsGlobalNamespace)
        {
            if (sb.Length > 0) sb.Insert(0, ".");
            sb.Insert(0, ns.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
        }

        return sb.ToString();
    }

    #endregion

    #region Kind & flags

    /// <summary>
    /// Maps Roslyn type kinds to domain <see cref="TypeKind"/>.
    /// </summary>
    public static DomainTypeKind ToDomainTypeKind(this INamedTypeSymbol t)
        => t.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Class => t.IsRecord ? DomainTypeKind.Class : DomainTypeKind.Class,
            Microsoft.CodeAnalysis.TypeKind.Struct => DomainTypeKind.Struct,
            Microsoft.CodeAnalysis.TypeKind.Interface => DomainTypeKind.Interface,
            Microsoft.CodeAnalysis.TypeKind.Enum => DomainTypeKind.Enum,
            _ => DomainTypeKind.Class
        };

    public static bool IsPublic(this ISymbol s) => s.DeclaredAccessibility == Accessibility.Public;

    public static bool IsAbstractType(this INamedTypeSymbol t) =>
        t.IsAbstract || t.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface;

    #endregion
}
