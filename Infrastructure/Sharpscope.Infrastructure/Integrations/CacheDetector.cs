using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class CacheDetector : IIntegrationDetector
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
            if (!TryMatchCacheConfig(entry.KeyPath, out var name, out var tech)) continue;

            var logical = string.IsNullOrWhiteSpace(name) ? "default" : name!;
            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Cache, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Cache, tech, logical);
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

        foreach (var pkg in context.Packages)
        {
            if (!TryMapCachePackage(pkg.Name, out var tech)) continue;

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
            if (!TryMapCacheInvocation(inv.MethodFullName, out var tech)) continue;

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
            if (!TryMapCacheType(type.TypeFullName, out var tech)) continue;

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
            if (existing.Technology == "Cache" && tech != "Cache")
                existing.Technology = tech;
            return existing;
        }

        var logical = "default";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Cache, logical);
        if (!candidates.TryGetValue(id, out var builder))
        {
            builder = new IntegrationCandidateBuilder(id, IntegrationKind.Cache, tech, logical);
            candidates[id] = builder;
        }
        else if (builder.Technology == "Cache" && tech != "Cache")
        {
            builder.Technology = tech;
        }

        return builder;
    }

    private static bool TryMatchCacheConfig(string keyPath, out string? name, out string tech)
    {
        name = null;
        tech = "Cache";
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("Redis", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            name = "redis";
            return true;
        }

        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var idx = Array.FindIndex(parts, p => p.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
                                             p.Equals("Caches", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            name = parts.Length > idx + 1 ? parts[idx + 1] : "default";
            return true;
        }

        return false;
    }

    private static bool TryMapCachePackage(string package, out string tech)
    {
        tech = "Cache";
        if (package.Contains("StackExchange.Redis", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("Microsoft.Extensions.Caching.StackExchangeRedis", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            return true;
        }

        if (package.Contains("Microsoft.Extensions.Caching.Redis", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            return true;
        }

        return false;
    }

    private static bool TryMapCacheInvocation(string methodFullName, out string tech)
    {
        tech = "Cache";
        if (methodFullName.Contains("AddStackExchangeRedisCache", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            return true;
        }
        if (methodFullName.Contains("AddDistributedRedisCache", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            return true;
        }
        return false;
    }

    private static bool TryMapCacheType(string typeFullName, out string tech)
    {
        tech = "Cache";
        if (typeFullName.Contains("IDistributedCache", StringComparison.OrdinalIgnoreCase))
        {
            tech = "DistributedCache";
            return true;
        }
        if (typeFullName.Contains("ConnectionMultiplexer", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            return true;
        }
        return false;
    }
}
