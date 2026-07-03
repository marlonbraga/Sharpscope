using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class KeyVaultDetector : IIntegrationDetector
{
    private const double ConfigWeight = 0.6;
    private const double InvocationWeight = 0.3;
    private const double TypeWeight = 0.2;
    private const double PackageWeight = 0.2;
    private const double SecretNameWeight = 0.2;
    private const double UnresolvedWeight = -0.1;

    // Pre-existing legacy debt: cognitive complexity exceeds the 15 allowed by the Code Quality
    // principle (constitution). Suppressed here rather than lowering the gate for everyone;
    // refactor this method (with a characterization test first, per Principle I) the next time
    // it needs to change.
#pragma warning disable S3776
    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);

        foreach (var entry in context.ConfigEntries)
        {
            if (!TryMatchKeyVaultConfig(entry, out var logical, out var endpoint))
                continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Secrets, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Secrets, "AzureKeyVault", logical);
                candidates[id] = builder;
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
            if (!TryMapKeyVaultPackage(pkg.Name)) continue;

            var builder = ResolveUsageBuilder(candidates);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.PackageReference,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, pkg.FilePath),
                Line: pkg.Line,
                Details: pkg.Name);

            builder.AddEvidence(evidence, PackageWeight, context);
        }

        foreach (var arg in context.InvocationArguments)
        {
            if (!TryMapKeyVaultInvocationArgument(arg.Target, out var role))
                continue;

            var builder = ResolveUsageBuilder(candidates);

            if (role == KeyVaultInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
            {
                if (string.IsNullOrWhiteSpace(builder.Endpoint))
                {
                    builder.Endpoint = arg.Value;
                    builder.EndpointSource ??= "Literal";
                }
            }
            else if (role == KeyVaultInvocationRole.SecretName)
            {
                var evidenceKind = arg.IsResolved ? IntegrationEvidenceKind.SecretName : IntegrationEvidenceKind.UnresolvedName;
                var weight = arg.IsResolved ? SecretNameWeight : UnresolvedWeight;
                var details = arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value) ? arg.Value! : arg.Target;

                var evidence = new IntegrationEvidence(
                    Kind: evidenceKind,
                    FilePath: null,
                    Line: null,
                    Details: details);

                builder.AddEvidence(evidence, weight, context, arg.NodeId);
                if (arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
                    builder.SetAttribute("SecretName", arg.Value!);
            }
        }

        foreach (var inv in context.Invocations)
        {
            if (!TryMapKeyVaultInvocation(inv.MethodFullName)) continue;

            var builder = ResolveUsageBuilder(candidates);

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.Invocation,
                FilePath: null,
                Line: null,
                Details: inv.MethodFullName);

            builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
        }

        foreach (var type in context.TypeUsages)
        {
            if (!TryMapKeyVaultType(type.TypeFullName)) continue;

            var builder = ResolveUsageBuilder(candidates);

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
#pragma warning restore S3776

    private static IntegrationCandidateBuilder ResolveUsageBuilder(
        IDictionary<string, IntegrationCandidateBuilder> candidates)
    {
        if (candidates.Count == 1)
            return candidates.Values.First();

        var logical = "keyvault";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Secrets, logical);
        if (!candidates.TryGetValue(id, out var builder))
        {
            builder = new IntegrationCandidateBuilder(id, IntegrationKind.Secrets, "AzureKeyVault", logical);
            candidates[id] = builder;
        }

        return builder;
    }

    private static bool TryMatchKeyVaultConfig(ConfigEntry entry, out string logical, out string? endpoint)
    {
        logical = "keyvault";
        endpoint = null;
        var keyPath = entry.KeyPath;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("KeyVault", StringComparison.OrdinalIgnoreCase) ||
            keyPath.Contains("VaultUri", StringComparison.OrdinalIgnoreCase) ||
            keyPath.Contains("VaultUrl", StringComparison.OrdinalIgnoreCase))
        {
            if (keyPath.Contains("Uri", StringComparison.OrdinalIgnoreCase) ||
                keyPath.Contains("Url", StringComparison.OrdinalIgnoreCase))
                endpoint = entry.Value;
            return true;
        }

        return false;
    }

    private static bool TryMapKeyVaultPackage(string package)
        => package.Contains("Azure.Security.KeyVault.Secrets", StringComparison.OrdinalIgnoreCase) ||
           package.Contains("Azure.Extensions.AspNetCore.Configuration.Secrets", StringComparison.OrdinalIgnoreCase);

    private static bool TryMapKeyVaultInvocation(string methodFullName)
        => methodFullName.Contains("SecretClient", StringComparison.OrdinalIgnoreCase) ||
           methodFullName.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
           methodFullName.Contains("SetSecret", StringComparison.OrdinalIgnoreCase);

    private static bool TryMapKeyVaultType(string typeFullName)
        => typeFullName.Contains("SecretClient", StringComparison.OrdinalIgnoreCase);

    private static bool TryMapKeyVaultInvocationArgument(string target, out KeyVaultInvocationRole role)
    {
        role = KeyVaultInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("SecretClient", StringComparison.OrdinalIgnoreCase))
        {
            role = KeyVaultInvocationRole.Endpoint;
            return true;
        }

        if (target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
            target.Contains("SetSecret", StringComparison.OrdinalIgnoreCase) ||
            target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
        {
            role = KeyVaultInvocationRole.SecretName;
            return true;
        }

        return false;
    }

    private enum KeyVaultInvocationRole
    {
        Other,
        Endpoint,
        SecretName
    }
}
