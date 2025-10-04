using System.Diagnostics;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Thin wrapper around <see cref="Process"/> to enable testability.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process with the given arguments and returns stdout/stderr and exit code.
    /// Throws <see cref="TimeoutException"/> if the process does not finish before <paramref name="timeout"/>.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        DirectoryInfo? workingDirectory,
        TimeSpan? timeout,
        CancellationToken ct);
}

/// <summary>
/// Result of an external process execution.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;
}
