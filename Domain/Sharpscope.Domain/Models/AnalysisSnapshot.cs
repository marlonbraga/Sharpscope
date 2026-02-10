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
