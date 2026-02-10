using System;
using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// External integrations inferred from the codebase.
/// </summary>
public sealed record IntegrationsSnapshot(
    IReadOnlyList<IntegrationCandidate> Candidates,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? UsageByNodeId = null
)
{
    public static IntegrationsSnapshot Empty { get; } =
        new(Array.Empty<IntegrationCandidate>(), new Dictionary<string, IReadOnlyList<string>>());
}

public sealed record IntegrationCandidate(
    string Id,
    IntegrationKind Kind,
    string Technology,
    string LogicalName,
    string? Endpoint,
    double Confidence,
    IReadOnlyList<IntegrationEvidence> Evidence
);

public sealed record IntegrationEvidence(
    IntegrationEvidenceKind Kind,
    string? FilePath,
    int? Line,
    string Details
);

public enum IntegrationKind
{
    Database,
    Cache,
    MessageBus,
    HttpApi,
    Storage
}

public enum IntegrationEvidenceKind
{
    RoslynSymbol,
    Invocation,
    ConfigKey,
    PackageReference,
    IaC
}
