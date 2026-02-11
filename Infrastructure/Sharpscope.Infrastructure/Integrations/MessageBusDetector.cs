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
    private const double EnvWeight = 0.2;
    private const double SecretWeight = 0.2;
    private const double InvocationLiteralWeight = 0.15;
    private const double UnresolvedWeight = -0.1;

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);
        var keyEvidenceByNode = CollectKeyEvidence(context);
        var entityNameTouched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in context.ConfigEntries)
        {
            if (!TryMatchBusConfig(entry, out var logical, out var tech, out var endpoint)) continue;

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

            if (string.IsNullOrWhiteSpace(builder.Endpoint) && !string.IsNullOrWhiteSpace(endpoint))
            {
                builder.Endpoint = endpoint;
                builder.EndpointSource ??= "Config";
            }

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

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.PackageReference,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, pkg.FilePath),
                Line: pkg.Line,
                Details: pkg.Name);

            foreach (var builder in ResolveUsageBuilders(candidates, tech))
                builder.AddEvidence(evidence, PackageWeight, context);
        }

        foreach (var arg in context.InvocationArguments)
        {
            if (!TryMapBusInvocationArgument(arg.Target, arg.ArgumentIndex, out var tech, out var role))
                continue;

            foreach (var builder in ResolveUsageBuilders(candidates, tech))
            {
                if (role == BusInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
                {
                    if (string.IsNullOrWhiteSpace(builder.Endpoint))
                    {
                        builder.Endpoint = arg.Value;
                        builder.EndpointSource ??= "Literal";
                    }
                }

                if (role == BusInvocationRole.EntityName)
                {
                    entityNameTouched.Add(builder.Id);
                    if (arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
                        builder.SetAttribute("EntityName", arg.Value!);
                }

                var evidenceKind = arg.IsResolved ? IntegrationEvidenceKind.Invocation : IntegrationEvidenceKind.UnresolvedName;
                var weight = arg.IsResolved ? InvocationLiteralWeight : UnresolvedWeight;

                var evidence = new IntegrationEvidence(
                    Kind: evidenceKind,
                    FilePath: null,
                    Line: null,
                    Details: arg.Target);

                builder.AddEvidence(evidence, weight, context, arg.NodeId);
                AddKeyEvidence(builder, arg.NodeId, keyEvidenceByNode, context);
            }
        }

        foreach (var inv in context.Invocations)
        {
            var hasEntityNameInvocation = TryMapBusEntityNameInvocation(inv.MethodFullName, out var entityTech);
            if (!TryMapBusInvocation(inv.MethodFullName, out var tech) && !hasEntityNameInvocation) continue;

            var effectiveTech = hasEntityNameInvocation ? entityTech : tech;

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.Invocation,
                FilePath: null,
                Line: null,
                Details: inv.MethodFullName);

            foreach (var builder in ResolveUsageBuilders(candidates, effectiveTech))
            {
                builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
                AddKeyEvidence(builder, inv.NodeId, keyEvidenceByNode, context);
                if (hasEntityNameInvocation)
                    entityNameTouched.Add(builder.Id);
            }
        }

        foreach (var type in context.TypeUsages)
        {
            if (!TryMapBusType(type.TypeFullName, out var tech)) continue;

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.RoslynSymbol,
                FilePath: null,
                Line: null,
                Details: type.TypeFullName);

            foreach (var builder in ResolveUsageBuilders(candidates, tech))
            {
                builder.AddEvidence(evidence, TypeWeight, context, type.NodeId);
                AddKeyEvidence(builder, type.NodeId, keyEvidenceByNode, context);
            }
        }

        foreach (var builder in candidates.Values)
        {
            if (!entityNameTouched.Contains(builder.Id)) continue;
            if (builder.Attributes.ContainsKey("EntityName")) continue;
            builder.SetAttribute("EntityName", "unresolved");
        }

        return candidates.Values
            .Select(c => c.Build())
            .Where(c => c.Confidence > 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<IntegrationCandidateBuilder> ResolveUsageBuilders(
        IDictionary<string, IntegrationCandidateBuilder> candidates,
        string tech)
    {
        if (candidates.Count == 0)
        {
            var logical = "default";
            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.MessageBus, logical);
            var builder = new IntegrationCandidateBuilder(id, IntegrationKind.MessageBus, tech, logical);
            candidates[id] = builder;
            return new[] { builder };
        }

        var byTech = candidates.Values
            .Where(c => string.Equals(c.Technology, tech, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byTech.Count > 0)
            return byTech;

        if (candidates.Count == 1)
        {
            var existing = candidates.Values.First();
            if (existing.Technology == "MessageBus" && tech != "MessageBus")
                existing.Technology = tech;
            return new[] { existing };
        }

        return candidates.Values.ToList();
    }

    private static bool TryMatchBusConfig(ConfigEntry entry, out string logical, out string tech, out string? endpoint)
    {
        logical = "default";
        tech = "MessageBus";
        endpoint = null;

        var keyPath = entry.KeyPath;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            logical = "servicebus";
            endpoint = entry.Value;
            return true;
        }

        if (keyPath.Contains("RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            logical = "rabbitmq";
            return true;
        }

        if (keyPath.Contains("EventGrid", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureEventGrid";
            logical = "eventgrid";
            if (keyPath.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
                endpoint = entry.Value;
            return true;
        }

        if (keyPath.Contains("MassTransit", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MassTransit";
            logical = "masstransit";
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

        if (keyPath.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
        {
            var kind = ConnectionStringClassifier.Classify(entry.Value);
            if (kind == ConnectionStringKind.ServiceBus)
            {
                tech = "AzureServiceBus";
                logical = "servicebus";
                endpoint = entry.Value;
                return true;
            }
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
        if (package.Contains("Azure.Messaging.EventGrid", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureEventGrid";
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
        if (methodFullName.Contains("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("BasicPublish", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("BasicConsume", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            return true;
        }
        if (methodFullName.Contains("CreateSender", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("CreateProcessor", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("CreateReceiver", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }
        if (methodFullName.Contains("ServiceBusClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }
        if (methodFullName.Contains("EventGridPublisherClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureEventGrid";
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

    private static bool TryMapBusEntityNameInvocation(string methodFullName, out string tech)
    {
        tech = "MessageBus";
        if (string.IsNullOrWhiteSpace(methodFullName)) return false;

        if (methodFullName.Contains("CreateSender", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("CreateProcessor", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("CreateReceiver", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            return true;
        }

        if (methodFullName.Contains("BasicPublish", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("BasicConsume", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
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
        if (typeFullName.Contains("EventGridPublisherClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureEventGrid";
            return true;
        }
        if (typeFullName.Contains("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
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

    private static bool TryMapBusInvocationArgument(
        string target,
        int argIndex,
        out string tech,
        out BusInvocationRole role)
    {
        tech = "MessageBus";
        role = BusInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("ServiceBusClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            role = BusInvocationRole.Endpoint;
            return argIndex == 0;
        }

        if (target.Contains("EventGridPublisherClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureEventGrid";
            role = BusInvocationRole.Endpoint;
            return argIndex == 0;
        }

        if (target.Contains("CreateSender", StringComparison.OrdinalIgnoreCase) ||
            target.Contains("CreateProcessor", StringComparison.OrdinalIgnoreCase) ||
            target.Contains("CreateReceiver", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureServiceBus";
            role = BusInvocationRole.EntityName;
            return argIndex == 0;
        }

        if (target.Contains("BasicPublish", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            role = BusInvocationRole.EntityName;
            return argIndex == 0;
        }

        if (target.Contains("BasicConsume", StringComparison.OrdinalIgnoreCase))
        {
            tech = "RabbitMQ";
            role = BusInvocationRole.EntityName;
            return argIndex == 0;
        }

        return false;
    }

    private static Dictionary<string, List<IntegrationEvidence>> CollectKeyEvidence(IntegrationDiscoveryContext context)
    {
        var dict = new Dictionary<string, List<IntegrationEvidence>>(StringComparer.Ordinal);

        foreach (var arg in context.InvocationArguments)
        {
            if (!arg.IsResolved || string.IsNullOrWhiteSpace(arg.Value)) continue;
            var key = arg.Value!;

            if (arg.Target.Contains("GetEnvironmentVariable", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsBusKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsBusKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
            }
            else if (arg.Target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
                     arg.Target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsBusKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.SecretName, null, null, key));
            }
        }

        return dict;
    }

    private static void AddKeyEvidence(
        IntegrationCandidateBuilder builder,
        string nodeId,
        IReadOnlyDictionary<string, List<IntegrationEvidence>> keyEvidenceByNode,
        IntegrationDiscoveryContext context)
    {
        if (!keyEvidenceByNode.TryGetValue(nodeId, out var list)) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in list)
        {
            var key = $"{(int)evidence.Kind}|{evidence.Details}";
            if (!seen.Add(key)) continue;

            var weight = evidence.Kind switch
            {
                IntegrationEvidenceKind.EnvVarKey => EnvWeight,
                IntegrationEvidenceKind.SecretName => SecretWeight,
                _ => ConfigWeight * 0.5
            };

            builder.AddEvidence(evidence, weight, context, nodeId);
        }
    }

    private static void AddEvidence(
        IDictionary<string, List<IntegrationEvidence>> dict,
        string nodeId,
        IntegrationEvidence evidence)
    {
        if (!dict.TryGetValue(nodeId, out var list))
        {
            list = new List<IntegrationEvidence>();
            dict[nodeId] = list;
        }

        list.Add(evidence);
    }

    private static bool IsBusKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("ServiceBus", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Rabbit", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Topic", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("EventGrid", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Kafka", StringComparison.OrdinalIgnoreCase);
    }

    private enum BusInvocationRole
    {
        Other,
        Endpoint,
        EntityName
    }
}
