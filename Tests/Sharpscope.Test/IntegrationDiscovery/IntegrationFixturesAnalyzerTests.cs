using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sharpscope.Adapters.CSharp;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Calculators;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Integrations;
using Sharpscope.Infrastructure.Sources;
using Shouldly;
using Xunit;

namespace Sharpscope.Test.IntegrationDiscovery;

[CollectionDefinition("IntegrationFixtures", DisableParallelization = true)]
public sealed class IntegrationFixturesCollection : ICollectionFixture<IntegrationFixturesSnapshot>
{
}

[Collection("IntegrationFixtures")]
public sealed class IntegrationFixturesAnalyzerTests
{
    private readonly IntegrationFixturesSnapshot _fixture;

    public IntegrationFixturesAnalyzerTests(IntegrationFixturesSnapshot fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Detects SQL Server from EF Core + ConnectionStrings")]
    public async Task Detects_SqlServer_From_EfCore_And_ConnectionString()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Database, "SqlServer", "SqlServerDb");

        AssertMinimumEvidence(candidate, "ConnectionStrings:SqlServerDb");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects Redis from AddStackExchangeRedisCache + config")]
    public async Task Detects_Redis_From_AddStackExchangeRedisCache_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Cache, "Redis", "redis");

        AssertMinimumEvidence(candidate, "Redis:Configuration");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects Azure Service Bus from client + config")]
    public async Task Detects_AzureServiceBus_From_ServiceBusClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.MessageBus, "AzureServiceBus", "servicebus");

        AssertMinimumEvidence(candidate, "ServiceBus:ConnectionString");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
        AssertAttribute(candidate, "EntityName");
    }

    [Fact(DisplayName = "Detects Azure Cosmos DB from CosmosClient + config")]
    public async Task Detects_AzureCosmos_From_CosmosClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Database, "CosmosDb", "cosmos");

        AssertMinimumEvidence(candidate, "Cosmos:Endpoint");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects Oracle from OracleConnection + config")]
    public async Task Detects_Oracle_From_OracleConnection_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Database, "Oracle", "oracle");

        AssertMinimumEvidence(candidate, "Oracle:ConnectionString");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects Azure Event Grid from publisher + config")]
    public async Task Detects_AzureEventGrid_From_Publisher_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.MessageBus, "AzureEventGrid", "eventgrid");

        AssertMinimumEvidence(candidate, "EventGrid:TopicEndpoint");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects Azure Key Vault from SecretClient + config")]
    public async Task Detects_AzureKeyVault_From_SecretClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Secrets, "AzureKeyVault", "keyvault");

        AssertMinimumEvidence(candidate, "KeyVault:VaultUri");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
    }

    [Fact(DisplayName = "Detects internal HttpApi from AddHttpClient + config")]
    public async Task Detects_HttpInternal_From_AddHttpClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.HttpApi, "HttpClient", "UsersApi");

        AssertMinimumEvidence(candidate, "HttpClients:UsersApi");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertAttributeValue(candidate, "scope", "internal");
    }

    [Fact(DisplayName = "Detects external HttpApi from AddHttpClient + config")]
    public async Task Detects_HttpExternal_From_AddHttpClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.HttpApi, "HttpClient", "Payments");

        AssertMinimumEvidence(candidate, "HttpClients:Payments");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertAttributeValue(candidate, "scope", "external");
    }

    [Fact(DisplayName = "Detects APIM from host + header usage")]
    public async Task Detects_Apim_From_Host_And_Header()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.HttpApi, "AzureApiManagement", "apim");

        AssertMinimumEvidence(candidate, "Apim:BaseUrl");
        candidate.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.Invocation &&
                                              e.Details.Contains("Ocp-Apim-Subscription-Key", StringComparison.OrdinalIgnoreCase));
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertAttributeValue(candidate, "scope", "external");
    }

    [Fact(DisplayName = "Detects Azure Storage from BlobServiceClient + config")]
    public async Task Detects_AzureStorage_From_BlobServiceClient_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Storage, "AzureBlob", "default");

        AssertMinimumEvidence(candidate, "Storage:ConnectionString");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
        AssertAttribute(candidate, "container");
        AssertRedacted(candidate.Endpoint);
    }

    [Fact(DisplayName = "Detects MassTransit from AddMassTransit + consumer")]
    public async Task Detects_MassTransit_From_AddMassTransit_And_Consumer()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.MessageBus, "MassTransit", "masstransit");

        AssertMinimumEvidence(candidate, "MassTransit");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
    }

    [Fact(DisplayName = "Detects OpenTelemetry from AddOpenTelemetry + config")]
    public async Task Detects_OpenTelemetry_From_AddOpenTelemetry_And_Config()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());
        var candidate = FindCandidate(snapshot, IntegrationKind.Observability, "OpenTelemetry", "opentelemetry");

        AssertMinimumEvidence(candidate, "OpenTelemetry");
        AssertConfidence(candidate);
        AssertUsage(snapshot, candidate);
    }

    [Fact(DisplayName = "Mixed.Small avoids cross-contamination across integrations")]
    public async Task MixedSmall_Avoids_CrossContamination()
    {
        var mixedPath = Path.Combine(GetFixturesRoot().FullName, "Samples", "Sample.Mixed.Small");
        var snapshot = await _fixture.AnalyzeAsync(new DirectoryInfo(mixedPath));

        var db = FindCandidate(snapshot, IntegrationKind.Database, "SqlServer", "MixedDb");
        var bus = FindCandidate(snapshot, IntegrationKind.MessageBus, "AzureServiceBus", "servicebus");
        var cache = FindCandidate(snapshot, IntegrationKind.Cache, "Redis", "redis");

        db.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.ConfigKey && e.Details.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
        bus.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.ConfigKey && e.Details.Contains("ServiceBus", StringComparison.OrdinalIgnoreCase));
        cache.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.ConfigKey && e.Details.Contains("Redis", StringComparison.OrdinalIgnoreCase));

        db.Endpoint.ShouldNotBeNull();
        db.Endpoint!.ShouldContain("Server=localhost", Case.Insensitive);
        bus.Endpoint.ShouldNotBeNull();
        bus.Endpoint!.ShouldContain("Endpoint=sb://", Case.Insensitive);

        AssertRedacted(db.Endpoint);
        AssertRedacted(bus.Endpoint);
        AssertRedacted(cache.Endpoint);
    }

    [Fact(DisplayName = "Snapshot JSON is redacted and usage is present")]
    public async Task SnapshotJson_IsRedacted_And_UsageIsPresent()
    {
        var snapshot = await _fixture.AnalyzeAsync(GetFixturesRoot());

        snapshot.Metadata.IntegrationProfile.ShouldBe("work");
        snapshot.Integrations.UsageByNodeId.ShouldNotBeNull();
        snapshot.Integrations.UsageByNodeId!.Count.ShouldBeGreaterThan(0);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        json.ShouldNotContain("SuperSecret", Case.Insensitive);
        json.ShouldNotContain("MongoSecret", Case.Insensitive);
        json.ShouldNotContain("RedisSecret", Case.Insensitive);
        json.ShouldNotContain("MixedSecret", Case.Insensitive);
        json.ShouldNotContain("CosmosSecret", Case.Insensitive);
        json.ShouldNotContain("OracleSecret", Case.Insensitive);
        json.ShouldNotContain("EventGridSecret", Case.Insensitive);
        json.ShouldNotContain("ApimSecret", Case.Insensitive);
    }

    private static DirectoryInfo GetFixturesRoot()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return new DirectoryInfo(Path.Combine(repoRoot, "Tests", "IntegrationFixtures"));
    }

    private static IntegrationCandidate FindCandidate(
        AnalysisSnapshot snapshot,
        IntegrationKind kind,
        string technology,
        string logicalName)
    {
        return snapshot.Integrations.Candidates.Single(c =>
            c.Kind == kind &&
            string.Equals(c.Technology, technology, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertConfidence(IntegrationCandidate candidate)
    {
        candidate.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    private static void AssertMinimumEvidence(IntegrationCandidate candidate, string configKeyContains)
    {
        candidate.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.PackageReference);
        candidate.Evidence.ShouldContain(e =>
            (e.Kind == IntegrationEvidenceKind.ConfigKey ||
             e.Kind == IntegrationEvidenceKind.EnvVarKey ||
             e.Kind == IntegrationEvidenceKind.SecretName) &&
            e.Details.Contains(configKeyContains, StringComparison.OrdinalIgnoreCase));
        candidate.Evidence.ShouldContain(e => e.Kind == IntegrationEvidenceKind.Invocation ||
                                             e.Kind == IntegrationEvidenceKind.RoslynSymbol);
    }

    private static void AssertUsage(AnalysisSnapshot snapshot, IntegrationCandidate candidate)
    {
        snapshot.Integrations.UsageByNodeId.ShouldNotBeNull();
        var usage = snapshot.Integrations.UsageByNodeId!;
        var nodes = usage
            .Where(kv => kv.Value.Any(v => string.Equals(v, candidate.Id, StringComparison.Ordinal)))
            .Select(kv => kv.Key)
            .ToList();
        nodes.ShouldNotBeEmpty();

        nodes.Any(id => snapshot.Graph.Nodes.TryGetValue(id, out var node) && node.Kind == GraphNodeKind.Method)
            .ShouldBeTrue();

        if (snapshot.Integrations.UsageByTypeId is not null)
        {
            var parentTypes = ResolveParents(snapshot.Graph, nodes, GraphNodeKind.Type);
            parentTypes.Any(id => snapshot.Integrations.UsageByTypeId!.TryGetValue(id, out var list) &&
                                  list.Contains(candidate.Id, StringComparer.Ordinal))
                .ShouldBeTrue();
        }

        if (snapshot.Integrations.UsageByNamespaceId is not null)
        {
            var parentNamespaces = ResolveParents(snapshot.Graph, nodes, GraphNodeKind.Namespace);
            parentNamespaces.Any(id => snapshot.Integrations.UsageByNamespaceId!.TryGetValue(id, out var list) &&
                                       list.Contains(candidate.Id, StringComparer.Ordinal))
                .ShouldBeTrue();
        }

        if (snapshot.Integrations.UsageByProjectId is not null)
        {
            var parentProjects = ResolveParents(snapshot.Graph, nodes, GraphNodeKind.Project);
            parentProjects.Any(id => snapshot.Integrations.UsageByProjectId!.TryGetValue(id, out var list) &&
                                     list.Contains(candidate.Id, StringComparer.Ordinal))
                .ShouldBeTrue();
        }
    }

    private static void AssertRedacted(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        endpoint.ShouldNotContain("SuperSecret", Case.Insensitive);
        endpoint.ShouldNotContain("MongoSecret", Case.Insensitive);
        endpoint.ShouldNotContain("RedisSecret", Case.Insensitive);
        endpoint.ShouldNotContain("MixedSecret", Case.Insensitive);
        endpoint.ShouldNotContain("CosmosSecret", Case.Insensitive);
        endpoint.ShouldNotContain("OracleSecret", Case.Insensitive);
        endpoint.ShouldNotContain("EventGridSecret", Case.Insensitive);

        AssertKeyRedacted(endpoint, "Password");
        AssertKeyRedacted(endpoint, "SharedAccessKey");
        AssertKeyRedacted(endpoint, "AccountKey");
        AssertKeyRedacted(endpoint, "Key");
    }

    private static void AssertKeyRedacted(string endpoint, string key)
    {
        var pattern = $"(^|[;,&?]){Regex.Escape(key)}\\s*=\\s*([^;,&]+)";
        var match = Regex.Match(endpoint, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return;

        match.Groups[2].Value.ShouldBe("***");
    }

    private static void AssertAttribute(IntegrationCandidate candidate, string key)
    {
        candidate.Attributes.ShouldNotBeNull();
        candidate.Attributes!.ContainsKey(key).ShouldBeTrue();
        candidate.Attributes![key].ShouldNotBeNullOrWhiteSpace();
    }

    private static void AssertAttributeValue(IntegrationCandidate candidate, string key, string expected)
    {
        candidate.Attributes.ShouldNotBeNull();
        candidate.Attributes!.ContainsKey(key).ShouldBeTrue();
        candidate.Attributes![key].ToLowerInvariant().ShouldBe(expected.ToLowerInvariant());
    }

    private static IReadOnlyList<string> ResolveParents(
        CodeGraph graph,
        IReadOnlyList<string> nodes,
        GraphNodeKind targetKind)
    {
        var parents = new List<string>();
        var lookup = graph.Edges
            .Where(e => e.Kind == GraphEdgeKind.Contains)
            .GroupBy(e => e.ToId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().FromId, StringComparer.Ordinal);

        foreach (var nodeId in nodes)
        {
            var current = nodeId;
            while (lookup.TryGetValue(current, out var parent))
            {
                current = parent;
                if (graph.Nodes.TryGetValue(current, out var node) && node.Kind == targetKind)
                {
                    parents.Add(current);
                    break;
                }
            }
        }

        return parents.Distinct(StringComparer.Ordinal).ToList();
    }
}

public sealed class IntegrationFixturesSnapshot
{
    private readonly ConcurrentDictionary<string, Lazy<Task<AnalysisSnapshot>>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<AnalysisSnapshot> AnalyzeAsync(DirectoryInfo root)
    {
        var fullPath = root.FullName;
        var lazy = _cache.GetOrAdd(fullPath, _ => new Lazy<Task<AnalysisSnapshot>>(() => BuildSnapshotAsync(root)));
        return lazy.Value;
    }

    private static async Task<AnalysisSnapshot> BuildSnapshotAsync(DirectoryInfo root)
    {
        if (!root.Exists)
            throw new DirectoryNotFoundException(root.FullName);

        var source = Substitute.For<ISourceProvider>();
        source.MaterializeFromLocalAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(root));

        var detector = Substitute.For<ILanguageDetector>();
        detector.DetectLanguageAsync(Arg.Any<DirectoryInfo>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>("csharp"));

        var loader = new RoslynWorkspaceLoader(allowMsbuild: true, PathFilters.Default());
        var adapter = new CSharpLanguageAdapter(loader, new CodeGraphBuilder());

        var useCase = new AnalyzeSolutionUseCase(
            source,
            detector,
            new[] { adapter },
            new MetricsEngine(),
            new IntegrationDiscoveryEngine());

        var request = new AnalyzeRequest(
            Path: root.FullName,
            RepoUrl: null,
            Format: "json",
            OutputPath: null,
            IntegrationProfile: "work");

        return await useCase.ExecuteAsync(request, CancellationToken.None);
    }
}
