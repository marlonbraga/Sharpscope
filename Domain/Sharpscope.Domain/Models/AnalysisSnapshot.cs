using System;
using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Root export object for Sharpscope analysis.
/// </summary>
public sealed record AnalysisSnapshot(
    AnalysisMetadata Metadata,
    CodeGraph Graph,
    MetricsSnapshot Metrics,
    IntegrationsSnapshot Integrations
);

/// <summary>
/// Temporal and traceability metadata.
/// </summary>
public sealed record AnalysisMetadata(
    string RepoUrlOrPath,
    string? CommitSha,
    string? Branch,
    DateTimeOffset TimestampUtc,
    string ToolVersion,
    string MetricsSchemaVersion,
    string IntegrationsSchemaVersion
);

/// <summary>
/// Placeholder for integrations output (stage 2).
/// </summary>
public sealed record IntegrationsSnapshot
{
    public static IntegrationsSnapshot Empty { get; } = new();
}
