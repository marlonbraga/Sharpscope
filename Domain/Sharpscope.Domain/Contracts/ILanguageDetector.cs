namespace Sharpscope.Domain.Contracts;

/// <summary>
/// Detects which languages are present in a source tree.
/// </summary>
public interface ILanguageDetector
{
    /// <summary>
    /// Returns a list of language identifiers found in the directory
    /// (e.g., ["csharp", "python"]).
    /// </summary>
    Task<IReadOnlyList<string>> DetectAsync(DirectoryInfo root, CancellationToken ct);
}
