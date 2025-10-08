namespace Sharpscope.Cli.Services;

public interface IInputNormalizer
{
    /// <summary>
    /// Normalize path/repo inputs. Treats both the same way:
    /// - If either looks like a Git repo URL => repo
    /// - Else if either exists as a local directory => path
    /// - Else returns (null, null)
    /// </summary>
    (string? path, string? repo) NormalizeSource(string? pathCandidate, string? repoCandidate);
}
