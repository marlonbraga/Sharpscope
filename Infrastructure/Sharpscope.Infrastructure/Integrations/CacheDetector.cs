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
    private const double EnvWeight = 0.2;
    private const double SecretWeight = 0.2;
    private const double InvocationLiteralWeight = 0.15;
    private const double UnresolvedWeight = -0.1;

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);
        var keyEvidenceByNode = CollectKeyEvidence(context);

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
            if (!TryMapCachePackage(pkg.Name, out var tech)) continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.PackageReference,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, pkg.FilePath),
                Line: pkg.Line,
                Details: pkg.Name);

            builder.AddEvidence(evidence, PackageWeight, context);
        }

        foreach (var arg in context.InvocationArguments)
        {
            if (!TryMapCacheInvocationArgument(arg.Target, arg.ArgumentIndex, out var tech, out var role))
                continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            if (role == CacheInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
            {
                if (string.IsNullOrWhiteSpace(builder.Endpoint))
                {
                    builder.Endpoint = arg.Value;
                    builder.EndpointSource ??= "Literal";
                }
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
            AddKeyEvidence(builder, inv.NodeId, keyEvidenceByNode, context);
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
            AddKeyEvidence(builder, type.NodeId, keyEvidenceByNode, context);
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

    private static bool TryMapCacheInvocationArgument(
        string target,
        int argIndex,
        out string tech,
        out CacheInvocationRole role)
    {
        tech = "Cache";
        role = CacheInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("ConnectionMultiplexer", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Redis";
            role = CacheInvocationRole.Endpoint;
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
                if (!IsCacheKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsCacheKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
            }
            else if (arg.Target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
                     arg.Target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsCacheKey(key)) continue;
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

    private static bool IsCacheKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("Redis", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Cache", StringComparison.OrdinalIgnoreCase);
    }

    private enum CacheInvocationRole
    {
        Other,
        Endpoint
    }
}
