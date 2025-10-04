using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Glob-based include/exclude filtering for relative paths.
/// - If there are any include patterns, a path must match at least one include AND not match any exclude.
/// - If there are no include patterns, every path is included unless it matches an exclude.
/// Globs support: *, **, ? with '/' as separator (Windows '\' is normalized).
/// Special handling: '**/' matches zero or more directories (the trailing slash is optional).
/// </summary>
public sealed class PathFilters
{
    #region Fields & Ctor

    private readonly IReadOnlyList<Regex> _includes;
    private readonly IReadOnlyList<Regex> _excludes;

    public PathFilters(IEnumerable<string>? includes = null, IEnumerable<string>? excludes = null)
    {
        _includes = Compile(includes);
        _excludes = Compile(excludes);
    }

    #endregion

    #region Public API

    public bool ShouldInclude(string relativePath)
    {
        if (relativePath is null) return false;

        var norm = NormalizePath(relativePath);

        var excluded = _excludes.Count > 0 && _excludes.Any(r => r.IsMatch(norm));
        if (excluded) return false;

        if (_includes.Count == 0) return true;

        return _includes.Any(r => r.IsMatch(norm));
    }

    /// <summary>
    /// Creates a default filter that excludes common build and VCS folders.
    /// </summary>
    public static PathFilters Default(IEnumerable<string>? moreExcludes = null)
    {
        var defaults = new[]
        {
            "**/bin/**",
            "**/obj/**",
            "**/.git/**",
            "**/.vs/**",
            "**/.idea/**",
            "**/node_modules/**"
        };

        var merged = moreExcludes is null ? defaults : defaults.Concat(moreExcludes);
        return new PathFilters(excludes: merged);
    }

    public static string NormalizePath(string path)
        => path.Replace('\\', '/');

    #endregion

    #region Helpers

    private static IReadOnlyList<Regex> Compile(IEnumerable<string>? globs)
    {
        if (globs is null) return Array.Empty<Regex>();

        var list = new List<Regex>();
        foreach (var g in globs.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var pattern = GlobToRegex(NormalizePath(g.Trim()));
            var rx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            list.Add(rx);
        }
        return list;
    }

    private static string GlobToRegex(string glob)
    {
        // Convert a glob into a regex with special care for '**/' (zero or more directories, slash optional).
        // Rules:
        //   - '**/'  -> (?:.*/)?     (zero or more directories, including none; slash inside the group)
        //   - '**'   -> .*           (any chars)
        //   - '*'    -> [^/]*        (any chars except '/')
        //   - '?'    -> [^/]         (single char except '/')
        // Everything else is regex-escaped.
        var rx = new System.Text.StringBuilder();
        rx.Append('^');

        for (int i = 0; i < glob.Length; i++)
        {
            char c = glob[i];

            if (c == '*')
            {
                // Lookahead for second '*'
                bool hasSecondStar = (i + 1 < glob.Length) && glob[i + 1] == '*';
                if (hasSecondStar)
                {
                    // Handle '**/' specially
                    bool followedBySlash = (i + 2 < glob.Length) && glob[i + 2] == '/';
                    if (followedBySlash)
                    {
                        rx.Append("(?:.*/)?");
                        i += 2; // skip '**/'
                        continue;
                    }
                    else
                    {
                        rx.Append(".*");
                        i += 1; // skip second '*'
                        continue;
                    }
                }
                else
                {
                    rx.Append("[^/]*");
                    continue;
                }
            }
            else if (c == '?')
            {
                rx.Append("[^/]");
                continue;
            }
            else
            {
                // Escape regex metacharacters
                if ("+()^$.{}[]|\\".IndexOf(c) >= 0)
                    rx.Append('\\');

                rx.Append(c);
            }
        }

        rx.Append('$');
        return rx.ToString();
    }

    #endregion
}
