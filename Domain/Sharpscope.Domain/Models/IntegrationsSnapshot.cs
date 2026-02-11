using System;
using System.Collections.Generic;

namespace Sharpscope.Domain.Models;

/// <summary>
/// External integrations inferred from the codebase.
/// </summary>
public sealed record IntegrationsSnapshot(
    IReadOnlyList<IntegrationCandidate> Candidates,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? UsageByNodeId = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? UsageByTypeId = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? UsageByNamespaceId = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? UsageByProjectId = null
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
    string? EndpointSource,
    double Confidence,
    IReadOnlyList<IntegrationEvidence> Evidence,
    IReadOnlyDictionary<string, string>? Attributes = null
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
    Storage,
    Secrets,
    Observability
}

public enum IntegrationEvidenceKind
{
    RoslynSymbol,
    Invocation,
    ConfigKey,
    PackageReference,
    IaC,
    EnvVarKey,
    SecretName,
    UnresolvedName
}
