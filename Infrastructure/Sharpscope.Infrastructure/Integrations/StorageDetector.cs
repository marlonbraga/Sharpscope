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

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);

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
            if (!TryMapStoragePackage(pkg.Name, out var tech)) continue;

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
            if (!TryMapStorageInvocation(inv.MethodFullName, out var tech)) continue;

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
            if (!TryMapStorageType(type.TypeFullName, out var tech)) continue;

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
}
