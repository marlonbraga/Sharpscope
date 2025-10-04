using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Minimal Git CLI wrapper. Delegates to <see cref="IProcessRunner"/>.
/// </summary>
public sealed class GitCli
{
    #region Fields & Ctor

    private readonly IProcessRunner _runner;
    private readonly string _gitExe;

    public GitCli(IProcessRunner runner, string gitExecutable = "git")
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _gitExe = string.IsNullOrWhiteSpace(gitExecutable) ? "git" : gitExecutable;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Clones a repository into <paramref name="destination"/>. Parent directory must exist.
    /// </summary>
    public async Task CloneAsync(
        string repoUrl,
        DirectoryInfo destination,
        CancellationToken ct,
        string? branch = null,
        TimeSpan? timeout = null,
        bool shallow = true)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
            throw new ArgumentException("Repository URL is required.", nameof(repoUrl));
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));

        destination.Parent?.Create();
        var args = BuildCloneArgs(repoUrl, destination, branch, shallow);

        var result = await _runner.RunAsync(_gitExe, args, destination.Parent, timeout ?? TimeSpan.FromMinutes(5), ct)
                                  .ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException($"git clone failed (exit {result.ExitCode}): {result.StdErr}".Trim());
    }

    #endregion

    #region Helpers

    private static string BuildCloneArgs(string repoUrl, DirectoryInfo dest, string? branch, bool shallow)
    {
        // git clone <url> "<dest>" [--depth 1] [-b <branch> --single-branch]
        var quotedDest = $"\"{dest.FullName}\"";
        var args = $"clone {repoUrl} {quotedDest}";

        if (shallow)
            args += " --depth 1";

        if (!string.IsNullOrWhiteSpace(branch))
            args += $" -b {branch} --single-branch";

        return args;
    }

    #endregion
}
