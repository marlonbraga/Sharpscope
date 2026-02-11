using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class OpenTelemetryDetector : IIntegrationDetector
{
    private const double ConfigWeight = 0.2;
    private const double InvocationWeight = 0.4;
    private const double TypeWeight = 0.1;
    private const double PackageWeight = 0.3;
    private const double EnvWeight = 0.2;

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);
        var keyEvidenceByNode = CollectKeyEvidence(context);

        foreach (var entry in context.ConfigEntries)
        {
            if (!IsOpenTelemetryKey(entry.KeyPath)) continue;

            var logical = "opentelemetry";
            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Observability, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Observability, "OpenTelemetry", logical);
                candidates[id] = builder;
            }

            if (string.IsNullOrWhiteSpace(builder.Endpoint) &&
                entry.KeyPath.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                builder.Endpoint = entry.Value;
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
            if (!IsOpenTelemetryPackage(pkg.Name)) continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Observability, "opentelemetry");
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Observability, "OpenTelemetry", "opentelemetry");
                candidates[id] = builder;
            }

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.PackageReference,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, pkg.FilePath),
                Line: pkg.Line,
                Details: pkg.Name);

            builder.AddEvidence(evidence, PackageWeight, context);
        }

        foreach (var inv in context.Invocations)
        {
            if (!IsOpenTelemetryInvocation(inv.MethodFullName)) continue;

            foreach (var builder in candidates.Values)
            {
                var evidence = new IntegrationEvidence(
                    Kind: IntegrationEvidenceKind.Invocation,
                    FilePath: null,
                    Line: null,
                    Details: inv.MethodFullName);

                builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
                AddKeyEvidence(builder, inv.NodeId, keyEvidenceByNode, context);
            }
        }

        foreach (var type in context.TypeUsages)
        {
            if (!IsOpenTelemetryType(type.TypeFullName)) continue;

            foreach (var builder in candidates.Values)
            {
                var evidence = new IntegrationEvidence(
                    Kind: IntegrationEvidenceKind.RoslynSymbol,
                    FilePath: null,
                    Line: null,
                    Details: type.TypeFullName);

                builder.AddEvidence(evidence, TypeWeight, context, type.NodeId);
                AddKeyEvidence(builder, type.NodeId, keyEvidenceByNode, context);
            }
        }

        return candidates.Values
            .Select(c => c.Build())
            .Where(c => c.Confidence > 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
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
                if (!IsOpenTelemetryKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsOpenTelemetryKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
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
                _ => ConfigWeight
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

    private static bool IsOpenTelemetryPackage(string name)
        => name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenTelemetryInvocation(string methodFullName)
    {
        if (string.IsNullOrWhiteSpace(methodFullName)) return false;
        return methodFullName.Contains("AddOpenTelemetry", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("AddOtlpExporter", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("AddConsoleExporter", StringComparison.OrdinalIgnoreCase) ||
               methodFullName.Contains("AddAzureMonitor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenTelemetryType(string typeFullName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName)) return false;
        return typeFullName.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ||
               typeFullName.Contains("TracerProviderBuilder", StringComparison.OrdinalIgnoreCase) ||
               typeFullName.Contains("MeterProviderBuilder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenTelemetryKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("OTEL_", StringComparison.OrdinalIgnoreCase);
    }
}
