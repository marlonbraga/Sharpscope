using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Sources;

namespace Sharpscope.Infrastructure.Integrations;

/// <summary>
/// Discovers external integrations from the canonical graph + source files.
/// </summary>
public sealed class IntegrationDiscoveryEngine : IIntegrationDiscoveryEngine
{
    public async Task<IntegrationsSnapshot> DiscoverAsync(
        CodeGraph graph,
        DirectoryInfo root,
        string? profile,
        CancellationToken ct)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (!root.Exists) return IntegrationsSnapshot.Empty;

        var discoveryProfile = IntegrationDiscoveryProfiles.Resolve(profile);
        var context = await IntegrationDiscoveryContext.CreateAsync(graph, root, ct).ConfigureAwait(false);

        var detectors = discoveryProfile.Name.Equals("work", StringComparison.OrdinalIgnoreCase)
            ? new IIntegrationDetector[]
            {
                new DatabaseDetector(),
                new CacheDetector(),
                new MessageBusDetector(),
                new HttpClientDetector(),
                new StorageDetector(),
                new KeyVaultDetector(),
                new OpenTelemetryDetector()
            }
            : Array.Empty<IIntegrationDetector>();

        var candidates = new List<IntegrationCandidate>();
        foreach (var detector in detectors)
            candidates.AddRange(detector.Detect(context));

        var merged = MergeCandidates(candidates);
        var filtered = merged
            .Where(c => discoveryProfile.Allows(c))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var usageByNodeId = FilterUsageByNodeId(context.BuildUsageByNodeId(), filtered);
        var usageAggregates = IntegrationUsageAggregator.Build(graph, usageByNodeId);

        return new IntegrationsSnapshot(
            filtered,
            usageByNodeId,
            usageAggregates.UsageByTypeId,
            usageAggregates.UsageByNamespaceId,
            usageAggregates.UsageByProjectId);
    }

    private static IReadOnlyList<IntegrationCandidate> MergeCandidates(IReadOnlyList<IntegrationCandidate> candidates)
    {
        var grouped = candidates
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var merged = new List<IntegrationCandidate>();
        foreach (var group in grouped)
        {
            var first = group.First();

            var evidence = group
                .SelectMany(c => c.Evidence)
                .GroupBy(e => $"{(int)e.Kind}|{e.FilePath}|{e.Line}|{e.Details}", StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(e => e.Kind)
                .ThenBy(e => e.FilePath, StringComparer.Ordinal)
                .ThenBy(e => e.Line)
                .ThenBy(e => e.Details, StringComparer.Ordinal)
                .ToList();

            var confidence = Math.Min(1.0, group.Max(c => c.Confidence));
            var endpoint = group.Select(c => c.Endpoint).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            var endpointSource = group.Select(c => c.EndpointSource).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            var attributes = MergeAttributes(group);

            merged.Add(new IntegrationCandidate(
                Id: first.Id,
                Kind: first.Kind,
                Technology: first.Technology,
                LogicalName: first.LogicalName,
                Endpoint: endpoint,
                EndpointSource: endpointSource,
                Confidence: confidence,
                Evidence: evidence,
                Attributes: attributes
            ));
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> FilterUsageByNodeId(
        IReadOnlyDictionary<string, IReadOnlyList<string>> usageByNodeId,
        IReadOnlyList<IntegrationCandidate> allowed)
    {
        if (usageByNodeId.Count == 0 || allowed.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>();

        var allowedIds = new HashSet<string>(allowed.Select(c => c.Id), StringComparer.Ordinal);
        var filtered = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var entry in usageByNodeId)
        {
            var list = entry.Value
                .Where(id => allowedIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (list.Count > 0)
                filtered[entry.Key] = list;
        }

        return filtered;
    }

    private static IReadOnlyDictionary<string, string>? MergeAttributes(IEnumerable<IntegrationCandidate> candidates)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (candidate.Attributes is null) continue;
            foreach (var kv in candidate.Attributes)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value)) continue;
                if (!dict.TryGetValue(kv.Key, out var existing))
                {
                    dict[kv.Key] = kv.Value;
                    continue;
                }

                if (string.Equals(existing, kv.Value, StringComparison.Ordinal)) continue;

                var combined = string.Join(",", new[] { existing, kv.Value }
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                dict[kv.Key] = combined;
            }
        }

        return dict.Count == 0 ? null : dict;
    }
}

internal sealed class IntegrationDiscoveryContext
{
    private readonly Dictionary<string, HashSet<string>> _usageByNodeId = new(StringComparer.Ordinal);

    public DirectoryInfo Root { get; }
    public CodeGraph Graph { get; }
    public IReadOnlyList<PackageReferenceInfo> Packages { get; }
    public IReadOnlyList<ConfigEntry> ConfigEntries { get; }
    public IReadOnlyList<InvocationInfo> Invocations { get; }
    public IReadOnlyList<TypeUsageInfo> TypeUsages { get; }
    public IReadOnlyList<InvocationArgumentInfo> InvocationArguments { get; }

    private IntegrationDiscoveryContext(
        DirectoryInfo root,
        CodeGraph graph,
        IReadOnlyList<PackageReferenceInfo> packages,
        IReadOnlyList<ConfigEntry> configEntries,
        IReadOnlyList<InvocationInfo> invocations,
        IReadOnlyList<TypeUsageInfo> typeUsages,
        IReadOnlyList<InvocationArgumentInfo> invocationArguments)
    {
        Root = root;
        Graph = graph;
        Packages = packages;
        ConfigEntries = configEntries;
        Invocations = invocations;
        TypeUsages = typeUsages;
        InvocationArguments = invocationArguments;
    }

    public static async Task<IntegrationDiscoveryContext> CreateAsync(
        CodeGraph graph,
        DirectoryInfo root,
        CancellationToken ct)
    {
        var packages = await PackageReferenceScanner.ScanAsync(root, ct).ConfigureAwait(false);
        var config = await ConfigScanner.ScanAsync(root, ct).ConfigureAwait(false);
        var invocations = GraphSignalScanner.CollectInvocations(graph);
        var typeUsages = GraphSignalScanner.CollectTypeUsages(graph);
        var invocationArguments = GraphSignalScanner.CollectInvocationArguments(graph);

        return new IntegrationDiscoveryContext(root, graph, packages, config, invocations, typeUsages, invocationArguments);
    }

    public void TrackUsage(string nodeId, string candidateId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(candidateId))
            return;

        if (!_usageByNodeId.TryGetValue(nodeId, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _usageByNodeId[nodeId] = set;
        }

        set.Add(candidateId);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> BuildUsageByNodeId()
    {
        if (_usageByNodeId.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>();

        return _usageByNodeId.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.OrderBy(v => v, StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);
    }
}

internal sealed record PackageReferenceInfo(string Name, string FilePath, int? Line);
internal sealed record ConfigEntry(string KeyPath, string? Value, string FilePath, int? Line);
internal sealed record InvocationInfo(string NodeId, string MethodFullName);
internal sealed record TypeUsageInfo(string NodeId, string TypeFullName);
internal sealed record InvocationArgumentInfo(
    string NodeId,
    string Target,
    int ArgumentIndex,
    string? Value,
    bool IsResolved);

internal interface IIntegrationDetector
{
    IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context);
}

internal static class IntegrationDiscoveryHelpers
{
    public static string NormalizeLogicalName(string? name)
    {
        var raw = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();
        var lower = raw.ToLowerInvariant();
        var chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var norm = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(norm) ? "default" : norm;
    }

    public static string BuildCandidateId(IntegrationKind kind, string logicalName)
        => $"ext:{kind.ToString().ToLowerInvariant()}:{NormalizeLogicalName(logicalName)}";

    public static string NormalizePath(DirectoryInfo root, string path)
    {
        try
        {
            var rel = Path.GetRelativePath(root.FullName, path);
            return PathFilters.NormalizePath(rel);
        }
        catch
        {
            return PathFilters.NormalizePath(path);
        }
    }

    public static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

internal static class PackageReferenceScanner
{
    public static Task<IReadOnlyList<PackageReferenceInfo>> ScanAsync(DirectoryInfo root, CancellationToken ct)
    {
        var files = EnumerateFiles(root,
            f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith("Directory.Packages.props", StringComparison.OrdinalIgnoreCase));

        var list = new List<PackageReferenceInfo>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
                var packages = doc.Descendants()
                    .Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(e.Name.LocalName, "PackageVersion", StringComparison.OrdinalIgnoreCase));

                foreach (var pkg in packages)
                {
                    var name = pkg.Attribute("Include")?.Value
                               ?? pkg.Attribute("Update")?.Value;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var lineInfo = pkg as IXmlLineInfo;
                    var line = lineInfo is not null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : (int?)null;
                    list.Add(new PackageReferenceInfo(name.Trim(), file, line));
                }
            }
            catch
            {
                // Ignore unreadable or malformed project files
            }
        }

        var ordered = list
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Line)
            .ToList();

        return Task.FromResult((IReadOnlyList<PackageReferenceInfo>)ordered);
    }

    private static IEnumerable<string> EnumerateFiles(DirectoryInfo root, Func<string, bool> predicate)
    {
        var filters = PathFilters.Default();
        return Directory.EnumerateFiles(root.FullName, "*.*", SearchOption.AllDirectories)
            .Where(path => filters.ShouldInclude(Path.GetRelativePath(root.FullName, path)))
            .Where(predicate)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
    }
}

internal static class ConfigScanner
{
    public static async Task<IReadOnlyList<ConfigEntry>> ScanAsync(DirectoryInfo root, CancellationToken ct)
    {
        var entries = new List<ConfigEntry>();
        var filters = PathFilters.Default();

        var jsonFiles = Directory.EnumerateFiles(root.FullName, "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => filters.ShouldInclude(Path.GetRelativePath(root.FullName, path)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in jsonFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                foreach (var entry in EnumerateJsonEntries(doc.RootElement, null))
                {
                    entries.Add(new ConfigEntry(entry.key, entry.value, file, Line: null));
                }
            }
            catch
            {
                // ignore invalid json
            }
        }

        var envFiles = Directory.EnumerateFiles(root.FullName, ".env*", SearchOption.AllDirectories)
            .Where(path => filters.ShouldInclude(Path.GetRelativePath(root.FullName, path)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in envFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var lines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    entries.Add(new ConfigEntry(key, value, file, i + 1));
                }
            }
            catch
            {
                // ignore unreadable env files
            }
        }

        return entries
            .OrderBy(e => e.KeyPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<(string key, string? value)> EnumerateJsonEntries(JsonElement element, string? path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var next = string.IsNullOrWhiteSpace(path) ? prop.Name : $"{path}:{prop.Name}";
                foreach (var entry in EnumerateJsonEntries(prop.Value, next))
                    yield return entry;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var item in element.EnumerateArray())
            {
                var next = string.IsNullOrWhiteSpace(path) ? idx.ToString() : $"{path}:{idx}";
                foreach (var entry in EnumerateJsonEntries(item, next))
                    yield return entry;
                idx++;
            }
        }
        else
        {
            yield return (path ?? string.Empty, element.ToString());
        }
    }
}

internal static class GraphSignalScanner
{
    public static IReadOnlyList<InvocationInfo> CollectInvocations(CodeGraph graph)
    {
        var list = new List<InvocationInfo>();
        foreach (var node in graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Method))
        {
            if (!node.Attributes.TryGetValue(GraphAttributeKeys.MethodExternalCalls, out var json))
                continue;

            var calls = IntegrationDiscoveryHelpers.DeserializeStringList(json);
            foreach (var call in calls)
            {
                if (string.IsNullOrWhiteSpace(call)) continue;
                list.Add(new InvocationInfo(node.Id, call));
            }
        }

        return list;
    }

    public static IReadOnlyList<TypeUsageInfo> CollectTypeUsages(CodeGraph graph)
    {
        var list = new List<TypeUsageInfo>();
        foreach (var node in graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Type))
        {
            if (!node.Attributes.TryGetValue(GraphAttributeKeys.DependsOnTypes, out var json))
                continue;

            var types = IntegrationDiscoveryHelpers.DeserializeStringList(json);
            foreach (var type in types)
            {
                if (string.IsNullOrWhiteSpace(type)) continue;
                list.Add(new TypeUsageInfo(node.Id, type));
            }
        }

        return list;
    }

    public static IReadOnlyList<InvocationArgumentInfo> CollectInvocationArguments(CodeGraph graph)
    {
        var list = new List<InvocationArgumentInfo>();
        foreach (var node in graph.Nodes.Values.Where(n => n.Kind == GraphNodeKind.Method))
        {
            if (!node.Attributes.TryGetValue(GraphAttributeKeys.MethodInvocationArguments, out var json))
                continue;

            if (string.IsNullOrWhiteSpace(json)) continue;

            List<InvocationArgumentPayload>? payloads;
            try
            {
                payloads = JsonSerializer.Deserialize<List<InvocationArgumentPayload>>(json);
            }
            catch
            {
                payloads = null;
            }

            if (payloads is null) continue;

            foreach (var p in payloads)
            {
                if (string.IsNullOrWhiteSpace(p.Target)) continue;
                list.Add(new InvocationArgumentInfo(
                    node.Id,
                    p.Target,
                    p.ArgumentIndex,
                    p.Value,
                    p.IsResolved));
            }
        }

        return list;
    }

    private sealed record InvocationArgumentPayload(
        string Target,
        int ArgumentIndex,
        string? Value,
        bool IsResolved);
}

internal sealed record UsageAggregates(
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsageByTypeId,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsageByNamespaceId,
    IReadOnlyDictionary<string, IReadOnlyList<string>> UsageByProjectId);

internal static class IntegrationUsageAggregator
{
    public static UsageAggregates Build(
        CodeGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<string>> usageByNodeId)
    {
        var byType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var byNamespace = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var byProject = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        if (usageByNodeId.Count == 0)
            return new UsageAggregates(new Dictionary<string, IReadOnlyList<string>>(),
                new Dictionary<string, IReadOnlyList<string>>(),
                new Dictionary<string, IReadOnlyList<string>>());

        var parentByChild = graph.Edges
            .Where(e => e.Kind == GraphEdgeKind.Contains)
            .GroupBy(e => e.ToId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().FromId, StringComparer.Ordinal);

        foreach (var entry in usageByNodeId)
        {
            if (!graph.Nodes.TryGetValue(entry.Key, out var node))
                continue;

            var current = entry.Key;
            var kind = node.Kind;

            if (kind == GraphNodeKind.Method)
            {
                var typeId = GetParent(current, parentByChild);
                Add(byType, typeId, entry.Value);

                var nsId = GetParent(typeId, parentByChild);
                Add(byNamespace, nsId, entry.Value);

                var projectId = GetParent(nsId, parentByChild);
                Add(byProject, projectId, entry.Value);
            }
            else if (kind == GraphNodeKind.Type)
            {
                Add(byType, current, entry.Value);
                var nsId = GetParent(current, parentByChild);
                Add(byNamespace, nsId, entry.Value);
                var projectId = GetParent(nsId, parentByChild);
                Add(byProject, projectId, entry.Value);
            }
            else if (kind == GraphNodeKind.Namespace)
            {
                Add(byNamespace, current, entry.Value);
                var projectId = GetParent(current, parentByChild);
                Add(byProject, projectId, entry.Value);
            }
            else if (kind == GraphNodeKind.Project)
            {
                Add(byProject, current, entry.Value);
            }
        }

        return new UsageAggregates(
            ToReadOnly(byType),
            ToReadOnly(byNamespace),
            ToReadOnly(byProject));
    }

    private static string? GetParent(string? child, IReadOnlyDictionary<string, string> parentByChild)
    {
        if (string.IsNullOrWhiteSpace(child)) return null;
        return parentByChild.TryGetValue(child, out var parent) ? parent : null;
    }

    private static void Add(
        IDictionary<string, HashSet<string>> dict,
        string? nodeId,
        IReadOnlyList<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        if (!dict.TryGetValue(nodeId!, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            dict[nodeId!] = set;
        }

        foreach (var candidate in candidates)
            set.Add(candidate);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ToReadOnly(
        IDictionary<string, HashSet<string>> dict)
    {
        return dict.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.OrderBy(v => v, StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);
    }
}
