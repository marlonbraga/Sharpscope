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
            if (!TryMatchHttpClientConfig(entry, out var logical, out var tech, out var endpoint))
                continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, tech, logical);
                candidates[id] = builder;
            }

            if (!string.IsNullOrWhiteSpace(endpoint))
                ApplyEndpoint(builder, endpoint, "Config", entry.KeyPath);

            if (builder.Technology == "HttpClient" && IsApimEndpoint(builder.Endpoint ?? endpoint))
                builder.Technology = "AzureApiManagement";
            if (IsApimName(builder.LogicalName))
                builder.Technology = "AzureApiManagement";

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.ConfigKey,
                FilePath: IntegrationDiscoveryHelpers.NormalizePath(context.Root, entry.FilePath),
                Line: entry.Line,
                Details: entry.KeyPath);

            builder.AddEvidence(evidence, ConfigWeight, context);
        }

        foreach (var pkg in context.Packages)
        {
            if (!IsHttpPackage(pkg.Name)) continue;

            var tech = pkg.Name.Contains("Grpc", StringComparison.OrdinalIgnoreCase) ? "Grpc" : "HttpClient";

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
            if (TryDetectApimHeader(arg, out var headerName))
            {
                var apimBuilder = ResolveApimBuilder(candidates);
                apimBuilder.Technology = "AzureApiManagement";

                var apimEvidence = new IntegrationEvidence(
                    Kind: IntegrationEvidenceKind.Invocation,
                    FilePath: null,
                    Line: null,
                    Details: headerName);

                apimBuilder.AddEvidence(apimEvidence, InvocationWeight, context, arg.NodeId);
                AddKeyEvidence(apimBuilder, arg.NodeId, keyEvidenceByNode, context);
            }

            if (!TryMapHttpInvocationArgument(arg.Target, arg.ArgumentIndex, out var tech, out var role))
                continue;

            var logical = role == HttpInvocationRole.LogicalName && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value)
                ? arg.Value!
                : "default";

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, tech, logical);
                candidates[id] = builder;
            }

            if (role == HttpInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
                ApplyEndpoint(builder, arg.Value, "Literal", null);

            if (builder.Technology == "HttpClient" && IsApimEndpoint(builder.Endpoint))
                builder.Technology = "AzureApiManagement";
            if (role == HttpInvocationRole.LogicalName && IsApimName(logical))
                builder.Technology = "AzureApiManagement";

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
            if (!IsHttpInvocation(inv.MethodFullName, out var tech)) continue;

            var evidence = new IntegrationEvidence(
                Kind: IntegrationEvidenceKind.Invocation,
                FilePath: null,
                Line: null,
                Details: inv.MethodFullName);

            foreach (var builder in ResolveUsageBuilders(candidates, tech))
            {
                builder.AddEvidence(evidence, InvocationWeight, context, inv.NodeId);
                AddKeyEvidence(builder, inv.NodeId, keyEvidenceByNode, context);
            }
        }

        foreach (var type in context.TypeUsages)
        {
            if (!IsHttpType(type.TypeFullName, out var tech)) continue;

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
            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
            var builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, tech, logical);
            candidates[id] = builder;
            return new[] { builder };
        }

        var byTech = candidates.Values
            .Where(c => string.Equals(c.Technology, tech, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byTech.Count > 0)
        {
            if (string.Equals(tech, "HttpClient", StringComparison.OrdinalIgnoreCase))
            {
                var apim = candidates.Values
                    .Where(c => string.Equals(c.Technology, "AzureApiManagement", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (apim.Count > 0)
                    byTech.AddRange(apim);
            }
            return byTech;
        }

        if (candidates.Count == 1)
        {
            var existing = candidates.Values.First();
            if (existing.Technology == "HttpClient" && tech == "Grpc")
                existing.Technology = tech;
            return new[] { existing };
        }

        return candidates.Values.ToList();
    }

    private static bool TryMatchHttpClientConfig(
        ConfigEntry entry,
        out string logical,
        out string tech,
        out string? endpoint)
    {
        logical = "default";
        tech = "HttpClient";
        endpoint = null;

        var keyPath = entry.KeyPath;
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
                logical = parts[idx + 1];
                endpoint = entry.Value;
                if (IsApimKey(keyPath))
                    tech = "AzureApiManagement";
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
                logical = parts.Length > idx + 1 ? parts[idx + 1] : "default";
                tech = "Grpc";
                endpoint = entry.Value;
                return true;
            }
        }

        idx = Array.FindIndex(parts, p => p.Equals("Api", StringComparison.OrdinalIgnoreCase) ||
                                         p.Equals("Apis", StringComparison.OrdinalIgnoreCase) ||
                                         p.Equals("Services", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && parts.Length >= idx + 1)
        {
            if (parts.Last().Equals("BaseUrl", StringComparison.OrdinalIgnoreCase) ||
                parts.Last().Equals("BaseAddress", StringComparison.OrdinalIgnoreCase) ||
                parts.Last().Equals("Url", StringComparison.OrdinalIgnoreCase))
            {
                logical = parts.Length > idx + 1 ? parts[idx + 1] : "default";
                endpoint = entry.Value;
                if (IsApimKey(keyPath))
                    tech = "AzureApiManagement";
                return true;
            }
        }

        if (IsApimKey(keyPath))
        {
            logical = "apim";
            tech = "AzureApiManagement";
            endpoint = entry.Value;
            return true;
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

    private static bool TryMapHttpInvocationArgument(
        string target,
        int argIndex,
        out string tech,
        out HttpInvocationRole role)
    {
        tech = "HttpClient";
        role = HttpInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("AddHttpClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "HttpClient";
            role = argIndex == 0 ? HttpInvocationRole.LogicalName : HttpInvocationRole.Other;
            return argIndex == 0;
        }

        if (target.Contains("GrpcChannel.ForAddress", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Grpc";
            role = HttpInvocationRole.Endpoint;
            return argIndex == 0;
        }

        if (target.Contains("System.Uri", StringComparison.OrdinalIgnoreCase))
        {
            tech = "HttpClient";
            role = HttpInvocationRole.Endpoint;
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
                if (!IsHttpKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsHttpKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
            }
            else if (arg.Target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
                     arg.Target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsHttpKey(key)) continue;
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

            if (!IsEvidenceForCandidate(builder, evidence))
                continue;

            builder.AddEvidence(evidence, weight, context, nodeId);

            if (builder.Technology == "HttpClient" &&
                IsApimKey(evidence.Details))
            {
                builder.Technology = "AzureApiManagement";
            }
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

    private static bool IsEvidenceForCandidate(IntegrationCandidateBuilder builder, IntegrationEvidence evidence)
    {
        var candidateKey = NormalizeToken(builder.LogicalName);
        if (candidateKey == "default") return true;

        var evidenceKey = TryExtractLogicalKey(evidence);
        if (!string.IsNullOrWhiteSpace(evidenceKey))
            return string.Equals(candidateKey, evidenceKey, StringComparison.OrdinalIgnoreCase);

        return evidence.Details.Contains(builder.LogicalName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractLogicalKey(IntegrationEvidence evidence)
    {
        var details = evidence.Details ?? string.Empty;

        if (evidence.Kind == IntegrationEvidenceKind.ConfigKey)
        {
            var parts = details.Split(':', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.FindIndex(parts, p => p.Equals("HttpClients", StringComparison.OrdinalIgnoreCase) ||
                                                 p.Equals("HttpClient", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && parts.Length > idx + 1)
                return NormalizeToken(parts[idx + 1]);

            idx = Array.FindIndex(parts, p => p.Equals("Api", StringComparison.OrdinalIgnoreCase) ||
                                             p.Equals("Apis", StringComparison.OrdinalIgnoreCase) ||
                                             p.Equals("Services", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && parts.Length > idx + 1)
                return NormalizeToken(parts[idx + 1]);

            if (IsApimKey(details))
                return NormalizeToken("apim");
        }

        if (evidence.Kind == IntegrationEvidenceKind.EnvVarKey ||
            evidence.Kind == IntegrationEvidenceKind.SecretName)
        {
            var raw = details.Replace(':', '_').Replace('-', '_');
            var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !IsNoiseToken(p))
                .ToArray();
            if (parts.Length > 0)
                return NormalizeToken(string.Join("", parts));
        }

        return null;
    }

    private static bool IsNoiseToken(string token)
    {
        var t = token.ToLowerInvariant();
        return t is "http" or "https" or "api" or "base" or "url" or "uri" or "endpoint" or "client";
    }

    private static string NormalizeToken(string value)
        => IntegrationDiscoveryHelpers.NormalizeLogicalName(value).Replace("-", string.Empty, StringComparison.Ordinal);

    private static bool IsApimEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return false;
        return endpoint.Contains("azure-api.net", StringComparison.OrdinalIgnoreCase) ||
               endpoint.Contains("apim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApimName(string? name)
        => !string.IsNullOrWhiteSpace(name) &&
           (name.Contains("apim", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("api-management", StringComparison.OrdinalIgnoreCase));

    private static bool IsApimKey(string keyPath)
        => keyPath.Contains("Apim", StringComparison.OrdinalIgnoreCase) ||
           keyPath.Contains("ApiManagement", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("Http", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Apim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDetectApimHeader(InvocationArgumentInfo arg, out string headerName)
    {
        headerName = string.Empty;
        if (!arg.IsResolved || string.IsNullOrWhiteSpace(arg.Value)) return false;
        if (arg.ArgumentIndex != 0) return false;
        if (!arg.Target.Contains("Headers.Add", StringComparison.OrdinalIgnoreCase)) return false;

        if (string.Equals(arg.Value, "Ocp-Apim-Subscription-Key", StringComparison.OrdinalIgnoreCase))
        {
            headerName = arg.Value;
            return true;
        }

        return false;
    }

    private static IntegrationCandidateBuilder ResolveApimBuilder(
        IDictionary<string, IntegrationCandidateBuilder> candidates)
    {
        var apim = candidates.Values.FirstOrDefault(c => IsApimName(c.LogicalName)) ??
                   candidates.Values.FirstOrDefault(c => string.Equals(c.Technology, "AzureApiManagement", StringComparison.OrdinalIgnoreCase));
        if (apim is not null)
            return apim;

        var logical = "apim";
        var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.HttpApi, logical);
        var builder = new IntegrationCandidateBuilder(id, IntegrationKind.HttpApi, "AzureApiManagement", logical);
        candidates[id] = builder;
        return builder;
    }

    private static void ApplyEndpoint(
        IntegrationCandidateBuilder builder,
        string? endpoint,
        string source,
        string? keyPath)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        var normalized = NormalizeHttpEndpoint(endpoint);
        if (string.IsNullOrWhiteSpace(builder.Endpoint))
        {
            builder.Endpoint = normalized;
            builder.EndpointSource ??= source;
        }

        var scope = DetermineScope(normalized, keyPath);
        if (!string.IsNullOrWhiteSpace(scope))
            builder.SetAttribute("scope", scope);
    }

    private static string? NormalizeHttpEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return endpoint;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return BuildEndpoint(uri);

        if (Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var fallback))
            return BuildEndpoint(fallback);

        return endpoint;
    }

    private static string BuildEndpoint(Uri uri)
    {
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.Host}{port}";
    }

    private static string? DetermineScope(string? endpoint, string? keyPath)
    {
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            if (keyPath.Contains("Internal", StringComparison.OrdinalIgnoreCase))
                return "internal";
            if (keyPath.Contains("External", StringComparison.OrdinalIgnoreCase))
                return "external";
        }

        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        if (!TryExtractHost(endpoint, out var host))
            return null;

        if (IsInternalHost(host))
            return "internal";

        return "external";
    }

    private static bool TryExtractHost(string endpoint, out string host)
    {
        host = string.Empty;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            return !string.IsNullOrWhiteSpace(host);
        }

        if (Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var fallback))
        {
            host = fallback.Host;
            return !string.IsNullOrWhiteSpace(host);
        }

        return false;
    }

    private static bool IsInternalHost(string host)
    {
        var lower = host.ToLowerInvariant();
        if (lower is "localhost") return true;
        if (lower.StartsWith("127.") || lower.StartsWith("10.") || lower.StartsWith("192.168."))
            return true;
        if (lower.StartsWith("172."))
        {
            var parts = lower.Split('.');
            if (parts.Length > 1 && int.TryParse(parts[1], out var second) && second is >= 16 and <= 31)
                return true;
        }

        if (lower.EndsWith(".local") ||
            lower.EndsWith(".svc") ||
            lower.EndsWith(".cluster.local") ||
            lower.EndsWith(".internal") ||
            lower.EndsWith(".localdomain"))
            return true;

        if (!lower.Contains('.', StringComparison.Ordinal))
            return true;

        return false;
    }

    private enum HttpInvocationRole
    {
        Other,
        LogicalName,
        Endpoint
    }
}
