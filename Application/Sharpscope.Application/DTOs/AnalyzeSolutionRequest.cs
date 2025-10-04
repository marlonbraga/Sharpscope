using System.Collections.Generic;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Input for analysis: either Path or RepoUrl must be provided.
/// </summary>
public sealed class AnalyzeSolutionRequest
{
    public string? Path { get; init; }
    public string? RepoUrl { get; init; }
    public AnalyzeSolutionOptions Options { get; init; } = new();
}