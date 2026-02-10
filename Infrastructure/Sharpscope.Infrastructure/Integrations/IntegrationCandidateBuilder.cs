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
    public double Confidence { get; private set; }

    public void AddEvidence(
        IntegrationEvidence evidence,
        double weight,
        IntegrationDiscoveryContext context,
        string? nodeId = null)
    {
        if (evidence is null) return;

        _evidence.Add(evidence);
        Confidence = Math.Min(1.0, Confidence + weight);

        if (!string.IsNullOrWhiteSpace(nodeId))
            context.TrackUsage(nodeId!, Id);
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
            Endpoint: Endpoint,
            Confidence: Confidence,
            Evidence: orderedEvidence
        );
    }
}
