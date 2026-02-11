using System;
using System.Collections.Generic;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class IntegrationDiscoveryProfile
{
    public IntegrationDiscoveryProfile(
        string name,
        IReadOnlySet<IntegrationKind> kinds,
        IReadOnlySet<string> technologies)
    {
        Name = name;
        EnabledKinds = kinds;
        EnabledTechnologies = technologies;
    }

    public string Name { get; }
    public IReadOnlySet<IntegrationKind> EnabledKinds { get; }
    public IReadOnlySet<string> EnabledTechnologies { get; }

    public bool Allows(IntegrationCandidate candidate)
    {
        if (candidate is null) return false;
        if (!EnabledKinds.Contains(candidate.Kind)) return false;
        return EnabledTechnologies.Contains(candidate.Technology);
    }
}

internal static class IntegrationDiscoveryProfiles
{
    private static readonly HashSet<IntegrationKind> WorkKinds = new()
    {
        IntegrationKind.Database,
        IntegrationKind.Cache,
        IntegrationKind.MessageBus,
        IntegrationKind.HttpApi,
        IntegrationKind.Storage,
        IntegrationKind.Secrets,
        IntegrationKind.Observability
    };

    private static readonly HashSet<string> WorkTechnologies = new(StringComparer.OrdinalIgnoreCase)
    {
        "SqlServer",
        "Oracle",
        "CosmosDb",
        "Redis",
        "AzureServiceBus",
        "AzureEventGrid",
        "AzureKeyVault",
        "MassTransit",
        "HttpClient",
        "AzureApiManagement",
        "AzureBlob",
        "Storage",
        "OpenTelemetry"
    };

    public static IntegrationDiscoveryProfile Work { get; } =
        new("work", WorkKinds, WorkTechnologies);

    public static IntegrationDiscoveryProfile Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Work;

        if (string.Equals(name.Trim(), "work", StringComparison.OrdinalIgnoreCase))
            return Work;

        throw new ArgumentException($"Unknown integration discovery profile '{name}'.");
    }
}
