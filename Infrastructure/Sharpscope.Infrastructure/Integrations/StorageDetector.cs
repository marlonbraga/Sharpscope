using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class StorageDetector : IIntegrationDetector
{
    private const double ConfigWeight = 0.6;
    private const double InvocationWeight = 0.3;
    private const double TypeWeight = 0.2;
    private const double PackageWeight = 0.2;
    private const double EnvWeight = 0.2;
    private const double SecretWeight = 0.2;
    private const double InvocationLiteralWeight = 0.15;
    private const double UnresolvedWeight = -0.1;

    // Pre-existing legacy debt: cognitive complexity exceeds the 15 allowed by the Code Quality
    // principle (constitution). Suppressed here rather than lowering the gate for everyone;
    // refactor this method (with a characterization test first, per Principle I) the next time
    // it needs to change.
#pragma warning disable S3776
    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);
        var keyEvidenceByNode = CollectKeyEvidence(context);

        foreach (var entry in context.ConfigEntries)
        {
            if (!TryMatchStorageConfig(entry.KeyPath, out var logical, out var tech)) continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Storage, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Storage, tech, logical);
                candidates[id] = builder;
            }
            else if (builder.Technology == "Storage" && tech != "Storage")
            {
                builder.Technology = tech;
            }

            if (string.IsNullOrWhiteSpace(builder.Endpoint) && !string.IsNullOrWhiteSpace(entry.Value))
            {
                ApplyEndpoint(builder, entry.Value, "Config");
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
            if (!TryMapStoragePackage(pkg.Name, out var tech)) continue;

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
            if (!TryMapStorageInvocationArgument(arg.Target, arg.ArgumentIndex, out var tech, out var role))
                continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            if (role == StorageInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
            {
                ApplyEndpoint(builder, arg.Value, "Literal");
            }

            if (role == StorageInvocationRole.ContainerName)
            {
                if (!string.IsNullOrWhiteSpace(arg.Value))
                {
                    builder.SetAttribute("container", arg.Value);
                }
                else if (!builder.Attributes.ContainsKey("container"))
                {
                    builder.SetAttribute("container", "unknown");
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
            if (!TryMapStorageInvocation(inv.MethodFullName, out var tech)) continue;

            var builder = ResolveUsageBuilder(candidates, tech);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.Invocation,
                FilePath: null,
                Line: null,
                Details: inv.MethodFullName);

            builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
            AddKeyEvidence(builder, inv.NodeId, keyEvidenceByNode, context);

            if (inv.MethodFullName.Contains("GetBlobContainerClient", StringComparison.OrdinalIgnoreCase) &&
                !builder.Attributes.ContainsKey("container"))
            {
                builder.SetAttribute("container", "unknown");
            }
        }

        foreach (var type in context.TypeUsages)
        {
            if (!TryMapStorageType(type.TypeFullName, out var tech)) continue;

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
#pragma warning restore S3776

    private static IntegrationCandidateBuilder ResolveUsageBuilder(
        IDictionary<string, IntegrationCandidateBuilder> candidates,
        string tech)
    {
        if (candidates.Count == 1)
        {
            var existing = candidates.Values.First();
            if (existing.Technology == "Storage" && tech != "Storage")
                existing.Technology = tech;
            return existing;
        }

        var logical = "default";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Storage, logical);
        if (!candidates.TryGetValue(id, out var builder))
        {
            builder = new IntegrationCandidateBuilder(id, IntegrationKind.Storage, tech, logical);
            candidates[id] = builder;
        }
        else if (builder.Technology == "Storage" && tech != "Storage")
        {
            builder.Technology = tech;
        }

        return builder;
    }

    private static bool TryMatchStorageConfig(string keyPath, out string logical, out string tech)
    {
        logical = "default";
        tech = "Storage";
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
            keyPath.Contains("Blob", StringComparison.OrdinalIgnoreCase) ||
            keyPath.Contains("S3", StringComparison.OrdinalIgnoreCase))
        {
            if (keyPath.Contains("Blob", StringComparison.OrdinalIgnoreCase))
                tech = "AzureBlob";
            if (keyPath.Contains("S3", StringComparison.OrdinalIgnoreCase))
                tech = "S3";
            return true;
        }

        return false;
    }

    private static bool TryMapStoragePackage(string package, out string tech)
    {
        tech = "Storage";
        if (package.Contains("Azure.Storage.Blobs", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            return true;
        }
        if (package.Contains("AWSSDK.S3", StringComparison.OrdinalIgnoreCase))
        {
            tech = "S3";
            return true;
        }
        if (package.Contains("Minio", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Minio";
            return true;
        }
        return false;
    }

    private static bool TryMapStorageInvocation(string methodFullName, out string tech)
    {
        tech = "Storage";
        if (methodFullName.Contains("GetBlobContainerClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            return true;
        }
        if (methodFullName.Contains("BlobServiceClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            return true;
        }
        if (methodFullName.Contains("AmazonS3Client", StringComparison.OrdinalIgnoreCase))
        {
            tech = "S3";
            return true;
        }
        if (methodFullName.Contains("MinioClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Minio";
            return true;
        }
        return false;
    }

    private static bool TryMapStorageType(string typeFullName, out string tech)
    {
        tech = "Storage";
        if (typeFullName.Contains("BlobServiceClient", StringComparison.OrdinalIgnoreCase) ||
            typeFullName.Contains("BlobContainerClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            return true;
        }
        if (typeFullName.Contains("AmazonS3Client", StringComparison.OrdinalIgnoreCase))
        {
            tech = "S3";
            return true;
        }
        if (typeFullName.Contains("MinioClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Minio";
            return true;
        }
        return false;
    }

    private static bool TryMapStorageInvocationArgument(
        string target,
        int argIndex,
        out string tech,
        out StorageInvocationRole role)
    {
        tech = "Storage";
        role = StorageInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("BlobServiceClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            role = StorageInvocationRole.Endpoint;
            return argIndex == 0;
        }

        if (target.Contains("GetBlobContainerClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "AzureBlob";
            role = StorageInvocationRole.ContainerName;
            return argIndex == 0;
        }

        return false;
    }

    // Pre-existing legacy debt: cognitive complexity exceeds the 15 allowed by the Code Quality
    // principle (constitution). Suppressed here rather than lowering the gate for everyone;
    // refactor this method (with a characterization test first, per Principle I) the next time
    // it needs to change.
#pragma warning disable S3776
    private static Dictionary<string, List<IntegrationEvidence>> CollectKeyEvidence(IntegrationDiscoveryContext context)
    {
        var dict = new Dictionary<string, List<IntegrationEvidence>>(StringComparer.Ordinal);

        foreach (var arg in context.InvocationArguments)
        {
            if (!arg.IsResolved || string.IsNullOrWhiteSpace(arg.Value)) continue;
            var key = arg.Value!;

            if (arg.Target.Contains("GetEnvironmentVariable", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsStorageKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsStorageKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
            }
            else if (arg.Target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
                     arg.Target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsStorageKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.SecretName, null, null, key));
            }
        }

        return dict;
    }
#pragma warning restore S3776

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

    private static bool IsStorageKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Blob", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("S3", StringComparison.OrdinalIgnoreCase);
    }

    private enum StorageInvocationRole
    {
        Other,
        Endpoint,
        ContainerName
    }

    private static void ApplyEndpoint(IntegrationCandidateBuilder builder, string? endpoint, string source)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        if (string.IsNullOrWhiteSpace(builder.Endpoint))
        {
            builder.Endpoint = NormalizeEndpoint(endpoint);
            builder.EndpointSource ??= source;
        }
    }

    private static string? NormalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return endpoint;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
            return $"{uri.Scheme}://{uri.Host}{port}";
        }

        return endpoint;
    }
}
