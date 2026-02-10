using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class MessageBusDetector : IIntegrationDetector
{
    private const double ConfigWeight = 0.6;
    private const double InvocationWeight = 0.3;
    private const double TypeWeight = 0.2;
    private const double PackageWeight = 0.2;

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);

        foreach (var entry in context.ConfigEntries)
        {
            if (!TryMatchBusConfig(entry.KeyPath, out var logical, out var tech)) continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.MessageBus, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.MessageBus, tech, logical);
                candidates[id] = builder;
            }
            else if (builder.Technology == "MessageBus" && tech != "MessageBus")
            {
                builder.Technology = tech;
            }

            if (string.IsNullOrWhiteSpace(builder.Endpoint) && !string.IsNullOrWhiteSpace(entry.Value))
                builder.Endpoint = entry.Value;

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.ConfigKey,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, entry.FilePath),
                Line: entry.Line,
                Details: entry.KeyPath);

            builder.AddEvidence(evidence, ConfigWeight, context);
        }

        foreach (var pkg in context.Packages)
        {
            if (!TryMapBusPackage(pkg.Name, out var tech)) continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.PackageReference,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, pkg.FilePath),
                Line: pkg.Line,
                Details: pkg.Name);

            builder.AddEvidence(evidence, PackageWeight, context);
        }

        foreach (var inv in context.Invocations)
        {
            if (!TryMapBusInvocation(inv.MethodFullName, out var tech)) continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.Invocation,
                FilePath: null,
                Line: null,
                Details: inv.MethodFullName);

            builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
        }

        foreach (var type in context.TypeUsages)
        {
            if (!TryMapBusType(type.TypeFullName, out var tech)) continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.RoslynSymbol,
                FilePath: null,
                Line: null,
                Details: type.TypeFullName);

            builder.AddEvidence(evidence, TypeWeight, context, type.NodeId);
        }

        return candidates.Values
            .Select(c => c.Build())
            .Where(c => c.Confidence > 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IntegrationCandidateBuilder ResolveUsageBuilder(
        IDictionary<string, IntegrationCandidateBuilder> candidates,
        string tech)
    {
        if (candidates.Count == 1)
        {
            var existing = candidates.Values.First();
            if (existing.Technology == "MessageBus" && tech != "MessageBus")
                existing.Technology = tech;
            return existing;
        }

        var logical = "default";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.MessageBus, logical);
        if (!candidates.TryGetValue(id, out var builder))
        {
            builder = new IntegrationCandidateBuilder(id, IntegrationKind.MessageBus, tech, logical);
            candidates[id] = builder;
        }
        else if (builder.Technology == "MessageBus" && tech != "MessageBus")
        {
            builder.Technology = tech;
        }

        return builder;
    }

    private static bool TryMatchBusConfig(string keyPath, out string logical, out string tech)
    {
        logical = "default";
        tech = "MessageBus";
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            logical = "servicebus";
            return true;
        }

        if (keyPath.Contains("RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            logical = "rabbitmq";
            return true;
        }

        if (keyPath.Contains("Kafka", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Kafka";
            logical = "kafka";
            return true;
        }

        if (keyPath.Contains("EventHubs", StringComparison.OrdinalIgnoreCase))
        {
            tech = "EventHubs";
            logical = "eventhubs";
            return true;
        }

        return false;
    }

    private static bool TryMapBusPackage(string package, out string tech)
    {
        tech = "MessageBus";
        if (package.Contains("Azure.Messaging.ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }
        if (package.Contains("MassTransit", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MassTransit";
            return true;
        }
        if (package.Contains("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            return true;
        }
        if (package.Contains("Confluent.Kafka", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Kafka";
            return true;
        }

        return false;
    }

    private static bool TryMapBusInvocation(string methodFullName, out string tech)
    {
        tech = "MessageBus";
        if (methodFullName.Contains("ServiceBusClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }
        if (methodFullName.Contains("AddMassTransit", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MassTransit";
            return true;
        }
        if (methodFullName.Contains("IPublishEndpoint.Publish", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("Publish", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MessageBus";
            return true;
        }
        if (methodFullName.Contains("Consume", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("Send", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MessageBus";
            return true;
        }
        return false;
    }

    private static bool TryMapBusType(string typeFullName, out string tech)
    {
        tech = "MessageBus";
        if (typeFullName.Contains("ServiceBusClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }
        if (typeFullName.Contains("IPublishEndpoint", StringComparison.OrdinalIgnoreCase) ||
            typeFullName.Contains("IBus", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MassTransit";
            return true;
        }
        if (typeFullName.Contains("IConsumer", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MassTransit";
            return true;
        }
        if (typeFullName.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            return true;
        }
        if (typeFullName.Contains("Kafka", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Kafka";
            return true;
        }
        return false;
    }
}
