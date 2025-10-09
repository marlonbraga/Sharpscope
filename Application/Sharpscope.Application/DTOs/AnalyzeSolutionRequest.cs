namespace Sharpscope.Application.DTOs;

/// <summary>
/// Input for analysis: either Path or RepoUrl must be provided.
/// </summary>
public sealed class AnalyzeSolutionRequest
{
    /// <summary>Local filesystem path (enabled/meaningful in localhost/dev).</summary>
    public string? Path { get; init; }

    /// <summary>Public Git repository URL (https://github.com/org/repo). For private, autenticação separada.</summary>
    public string? RepoUrl { get; init; }

    /// <summary>Options controlling formats and output naming.</summary>
    public AnalyzeSolutionOptions Options { get; init; } = new();
}
