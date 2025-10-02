using Sharpscope.Domain.Models;

namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Language-specific adapter that parses a source tree and produces the
/// language-agnostic IR <see cref="CodeModel"/>.
/// </summary>
public interface ILanguageAdapter
{
    /// <summary>
    /// Language identifier handled by this adapter (e.g., "csharp", "java").
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Returns true if this adapter can handle the given language id.
    /// </summary>
    bool CanHandle(string languageId);

    /// <summary>
    /// Builds a <see cref="CodeModel"/> from the given root directory.
    /// The directory should already contain the materialized source code.
    /// </summary>
    Task<CodeModel> BuildModelAsync(DirectoryInfo root, CancellationToken ct);
}
