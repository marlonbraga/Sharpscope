using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class IntegrationCandidateBuilder
{
    private readonly List<IntegrationEvidence> _evidence = new();

    public IntegrationCandidateBuilder(
        string id,
        IntegrationKind kind,
        string technology,
        string logicalName)
    {
        Id = id;
        Kind = kind;
        Technology = technology;
        LogicalName = logicalName;
    }

    public string Id { get; }
    public IntegrationKind Kind { get; }
    public string Technology { get; set; }
    public string LogicalName { get; }
    public string? Endpoint { get; set; }
    public string? EndpointSource { get; set; }
    public double Confidence { get; private set; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

    public void AddEvidence(
        IntegrationEvidence evidence,
        double weight,
        IntegrationDiscoveryContext context,
        string? nodeId = null)
    {
        if (evidence is null) return;

        _evidence.Add(evidence);
        Confidence = Math.Clamp(Confidence + weight, 0.0, 1.0);

        if (!string.IsNullOrWhiteSpace(nodeId))
            context.TrackUsage(nodeId!, Id);
    }

    public void SetAttribute(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;

        if (Attributes.TryGetValue(key, out var existing))
        {
            if (string.Equals(existing, value, StringComparison.Ordinal))
                return;

            var combined = string.Join(",", new[] { existing, value }
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            Attributes[key] = combined;
            return;
        }

        Attributes[key] = value;
    }

    public IntegrationCandidate Build()
    {
        var orderedEvidence = _evidence
            .GroupBy(e => $"{(int)e.Kind}|{e.FilePath}|{e.Line}|{e.Details}", StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(e => e.Kind)
            .ThenBy(e => e.FilePath, StringComparer.Ordinal)
            .ThenBy(e => e.Line)
            .ThenBy(e => e.Details, StringComparer.Ordinal)
            .ToList();

        return new IntegrationCandidate(
            Id: Id,
            Kind: Kind,
            Technology: Technology,
            LogicalName: LogicalName,
            Endpoint: IntegrationSecretRedactor.Redact(Endpoint),
            EndpointSource: EndpointSource,
            Confidence: Confidence,
            Evidence: orderedEvidence,
            Attributes: Attributes.Count == 0 ? null : new Dictionary<string, string>(Attributes, StringComparer.Ordinal)
        );
    }
}
