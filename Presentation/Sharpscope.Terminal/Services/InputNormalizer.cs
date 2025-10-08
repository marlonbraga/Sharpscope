using System.Text.RegularExpressions;

namespace Sharpscope.Cli.Services;

public sealed class InputNormalizer : IInputNormalizer
{
    private static readonly Regex GitUrlRegex = new(
        @"^(?:https?|git|ssh)://|^(?:git@)[\w\.-]+:.*?(/|:)\S+\.git$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GithubShorthand = new(
        @"^[\w\-]+/[\w\.-]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public (string? path, string? repo) NormalizeSource(string? pathCandidate, string? repoCandidate)
    {
        var candidates = new[] { pathCandidate, repoCandidate }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var c in candidates)
        {
            if (LooksLikeRepoUrl(c, out var normalized))
                return (null, normalized);

            if (Directory.Exists(c))
                return (c, null);
        }

        // Support GitHub shorthand "owner/repo"
        foreach (var c in candidates)
        {
            if (GithubShorthand.IsMatch(c))
                return (null, $"https://github.com/{c}.git");
        }

        return (null, null);
    }

    private static bool LooksLikeRepoUrl(string value, out string normalized)
    {
        normalized = value;

        // Plain https GitHub (with or without .git)
        if (value.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = EnsureGitSuffix(value);
            return true;
        }

        // Generic git/ssh url patterns (git@github.com:org/repo.git, ssh://, git://, etc.)
        if (GitUrlRegex.IsMatch(value))
            return true;

        return false;
    }

    private static string EnsureGitSuffix(string url) =>
        url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url : $"{url}.git";
}
