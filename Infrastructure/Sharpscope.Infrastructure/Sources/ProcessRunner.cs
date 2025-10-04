using System.Diagnostics;
using System.Text;

namespace Sharpscope.Infrastructure.Sources;

/// <summary>
/// Default implementation of <see cref="IProcessRunner"/> using <see cref="Process"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    #region Public API

    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        DirectoryInfo? workingDirectory,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Executable file name is required.", nameof(fileName));

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory?.FullName ?? Environment.CurrentDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            if (!proc.Start())
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = timeout.HasValue
                ? new CancellationTokenSource(timeout.Value)
                : null;

            using var linked = timeoutCts is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(proc);
                if (timeoutCts?.IsCancellationRequested == true)
                    throw new TimeoutException($"Process '{fileName}' timed out after {timeout}.");
                throw;
            }

            // Ensure async readers are done
            proc.WaitForExit();

            return new ProcessResult(proc.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch
        {
            if (!proc.HasExited)
                TryKill(proc);
            throw;
        }
    }

    #endregion

    #region Helpers

    private static void TryKill(Process proc)
    {
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
        catch { /* swallow best-effort */ }
    }

    #endregion
}
