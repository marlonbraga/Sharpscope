using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Contracts;
using Sharpscope.Infrastructure.Sources;

namespace Sharpscope.Infrastructure.Detection;

/// <summary>
/// Detects the primary language by file extension, honoring filters (bin/obj/.git/etc).
/// Returns the language id (e.g., "csharp") if exactly one language is found; otherwise null.
/// </summary>
public sealed class SimpleExtensionLanguageDetector : ILanguageDetector
{
    private readonly PathFilters _filters;

    public SimpleExtensionLanguageDetector(PathFilters? filters = null)
    {
        _filters = filters ?? PathFilters.Default();
    }

    public Task<string?> DetectLanguageAsync(DirectoryInfo root, CancellationToken ct)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (!root.Exists) throw new DirectoryNotFoundException(root.FullName);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "csharp",
            [".ts"] = "typescript",
            [".js"] = "javascript",
            [".java"] = "java",
            [".py"] = "python",
            [".go"] = "go",
            [".rb"] = "ruby",
        };

        var languagesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root.FullName, "*.*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var rel = Path.GetRelativePath(root.FullName, file);
            if (!_filters.ShouldInclude(PathFilters.NormalizePath(rel))) continue;

            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext)) continue;

            if (map.TryGetValue(ext, out var lang))
            {
                languagesSeen.Add(lang);

                // Se já é ambíguo, podemos parar cedo (o resultado final será null).
                if (languagesSeen.Count > 1)
                    break;
            }
        }

        var result = languagesSeen.Count == 1 ? languagesSeen.First() : null;
        return Task.FromResult<string?>(result);
    }
}
