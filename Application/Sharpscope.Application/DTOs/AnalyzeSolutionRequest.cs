namespace Sharpscope.Application.DTOs;

/// <summary>
/// Input for analysis: either Path or RepoUrl must be provided.
/// </summary>
public sealed class AnalyzeSolutionRequest
{
    /// <summary>Public Git repository URL (https://github.com/org/repo). </summary>
    public string? RepoUrl { get; init; }

    /// <summary>Options controlling formats and output naming.</summary>
    public AnalyzeSolutionOptions Options { get; init; } = new();
}
