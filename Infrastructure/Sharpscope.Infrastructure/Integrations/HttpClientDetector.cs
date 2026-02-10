using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class HttpClientDetector : IIntegrationDetector
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
            if (TryMatchHttpClientConfig(entry.KeyPath, out var name, out var isGrpc))
            {
                var tech = isGrpc ? "Grpc" : "HttpClient";
                var logical = string.IsNullOrWhiteSpace(name) ? "default" : name!;
                var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
                if (!candidates.TryGetValue(id, out var builder))
                {
                    builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, tech, logical);
                    candidates[id] = builder;
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
        }

        foreach (var pkg in context.Packages)
        {
            if (!IsHttpPackage(pkg.Name)) continue;

            var tech = pkg.Name.Contains("Grpc", StringComparison.OrdinalIgnoreCase) ? "Grpc" : "HttpClient";
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
            if (!IsHttpInvocation(inv.MethodFullName, out var tech)) continue;

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
            if (!IsHttpType(type.TypeFullName, out var tech)) continue;

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
            if (existing.Technology == "HttpClient" && tech == "Grpc")
                existing.Technology = tech;
            return existing;
        }

        var logical = "default";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
        if (!candidates.TryGetValue(id, out var builder))
        {
            builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, tech, logical);
            candidates[id] = builder;
        }
        else if (builder.Technology == "HttpClient" && tech == "Grpc")
        {
            builder.Technology = tech;
        }

        return builder;
    }

    private static bool TryMatchHttpClientConfig(string keyPath, out string? name, out bool isGrpc)
    {
        name = null;
        isGrpc = false;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        var idx = Array.FindIndex(parts, p => p.Equals("HttpClients", StringComparison.OrdinalIgnoreCase) ||
                                             p.Equals("HttpClient", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && parts.Length >= idx + 2)
        {
            if (parts.Last().Equals("BaseUrl", StringComparison.OrdinalIgnoreCase) ||
                parts.Last().Equals("BaseAddress", StringComparison.OrdinalIgnoreCase))
            {
                name = parts[idx + 1];
                return true;
            }
        }

        idx = Array.FindIndex(parts, p => p.Equals("Grpc", StringComparison.OrdinalIgnoreCase) ||
                                         p.Equals("GrpcClients", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && parts.Length >= idx + 1)
        {
            if (parts.Last().Equals("Address", StringComparison.OrdinalIgnoreCase) ||
                parts.Last().Equals("BaseUrl", StringComparison.OrdinalIgnoreCase))
            {
                name = parts.Length > idx + 1 ? parts[idx + 1] : "default";
                isGrpc = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsHttpPackage(string package)
        => package.Contains("Microsoft.Extensions.Http", StringComparison.OrdinalIgnoreCase) ||
           package.Contains("System.Net.Http", StringComparison.OrdinalIgnoreCase) ||
           package.Contains("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpInvocation(string methodFullName, out string tech)
    {
        tech = "HttpClient";
        if (methodFullName.Contains("AddHttpClient", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("HttpClient", StringComparison.OrdinalIgnoreCase))
            return true;

        if (methodFullName.Contains("GrpcChannel.ForAddress", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Grpc";
            return true;
        }

        return false;
    }

    private static bool IsHttpType(string typeFullName, out string tech)
    {
        tech = "HttpClient";
        if (typeFullName.Contains("System.Net.Http.HttpClient", StringComparison.OrdinalIgnoreCase))
            return true;

        if (typeFullName.Contains("Grpc.Net.Client.GrpcChannel", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Grpc";
            return true;
        }

        return false;
    }
}
