using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Detects the primary language of a materialized source tree (e.g., "csharp").
/// </summary>
public interface ILanguageDetector
{
    /// <summary>
    /// Returns a lowercase language id (e.g., "csharp"), or null if unknown/ambiguous.
    /// </summary>
    Task<string?> DetectLanguageAsync(DirectoryInfo root, CancellationToken ct);
}
