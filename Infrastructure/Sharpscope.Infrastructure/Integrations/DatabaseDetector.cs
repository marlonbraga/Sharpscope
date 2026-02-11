using System;
using System.Collections.Generic;
using System.Linq;
using Sharpscope.Domain.Models;

namespace Sharpscope.Infrastructure.Integrations;

internal sealed class DatabaseDetector : IIntegrationDetector
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
            if (!TryMatchDbConfig(entry, out var logical, out var tech, out var endpoint))
                continue;

            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Database, logical);
            if (!candidates.TryGetValue(id, out var builder))
            {
                builder = new IntegrationCandidateBuilder(id, IntegrationKind.Database, tech, logical);
                candidates[id] = builder;
            }
            else if (builder.Technology == "Database" && tech != "Database")
            {
                builder.Technology = tech;
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
            if (!TryMapDbPackage(pkg.Name, out var tech)) continue;

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
            if (!TryMapDbInvocationArgument(arg.Target, arg.ArgumentIndex, out var tech, out var role))
                continue;

            foreach (var builder in ResolveUsageBuilders(candidates, tech))
            {
                if (role == DbInvocationRole.Endpoint && arg.IsResolved && !string.IsNullOrWhiteSpace(arg.Value))
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
        }

        foreach (var inv in context.Invocations)
        {
            if (!TryMapDbInvocation(inv.MethodFullName, out var tech)) continue;

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
            if (!TryMapDbType(type.TypeFullName, out var tech)) continue;

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
            var id = IntegrationDiscoveryHelpers.BuildCandidateId(IntegrationKind.Database, logical);
            var builder = new IntegrationCandidateBuilder(id, IntegrationKind.Database, tech, logical);
            candidates[id] = builder;
            return new[] { builder };
        }

        var byTech = candidates.Values
            .Where(c => string.Equals(c.Technology, tech, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byTech.Count > 0)
            return byTech;

        if (candidates.Count == 1)
        {
            var existing = candidates.Values.First();
            if (existing.Technology == "Database" && tech != "Database")
                existing.Technology = tech;
            return new[] { existing };
        }

        return candidates.Values.ToList();
    }

    private static bool TryMatchConnectionString(string keyPath, out string? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var idx = Array.FindIndex(parts, p => p.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase) ||
                                             p.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        name = parts.Length > idx + 1 ? parts[idx + 1] : "default";
        return true;
    }

    private static bool TryMatchDbConfig(ConfigEntry entry, out string logical, out string tech, out string? endpoint)
    {
        logical = "default";
        tech = "Database";
        endpoint = null;

        string? name;
        if (TryMatchMongoConfig(entry.KeyPath, out name))
        {
            logical = string.IsNullOrWhiteSpace(name) ? "default" : name!;
            tech = "MongoDB";
            endpoint = entry.Value;
            return true;
        }

        if (TryMatchCosmosConfig(entry.KeyPath, out name))
        {
            logical = string.IsNullOrWhiteSpace(name) ? "cosmos" : name!;
            tech = "CosmosDb";
            if (entry.KeyPath.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                entry.KeyPath.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase))
                endpoint = entry.Value;
            return true;
        }

        if (TryMatchOracleConfig(entry.KeyPath, out name))
        {
            logical = string.IsNullOrWhiteSpace(name) ? "oracle" : name!;
            tech = "Oracle";
            endpoint = entry.Value;
            return true;
        }

        if (!TryMatchConnectionString(entry.KeyPath, out name)) return false;

        var kind = ConnectionStringClassifier.Classify(entry.Value);
        if (kind == ConnectionStringKind.ServiceBus || kind == ConnectionStringKind.Storage)
            return false;

        tech = kind switch
        {
            ConnectionStringKind.SqlServer => "SqlServer",
            ConnectionStringKind.PostgreSQL => "PostgreSQL",
            ConnectionStringKind.MySql => "MySql",
            ConnectionStringKind.Sqlite => "Sqlite",
            ConnectionStringKind.Oracle => "Oracle",
            ConnectionStringKind.MongoDB => "MongoDB",
            ConnectionStringKind.CosmosDb => "CosmosDb",
            _ => "Database"
        };

        logical = string.IsNullOrWhiteSpace(name) ? "default" : name!;
        endpoint = entry.Value;
        return true;
    }

    private static bool TryMatchMongoConfig(string keyPath, out string? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (parts[0].Equals("Mongo", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("MongoDb", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("MongoDB", StringComparison.OrdinalIgnoreCase))
        {
            name = parts.Length > 1 ? parts[1] : "default";
            return true;
        }

        return false;
    }

    private static bool TryMatchCosmosConfig(string keyPath, out string? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (parts[0].Equals("Cosmos", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("CosmosDb", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("CosmosDB", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length > 1)
            {
                var candidate = parts[1];
                if (candidate.Equals("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase))
                {
                    name = "cosmos";
                }
                else
                {
                    name = candidate;
                }
            }
            else
            {
                name = "cosmos";
            }
            return true;
        }

        return false;
    }

    private static bool TryMatchOracleConfig(string keyPath, out string? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(keyPath)) return false;

        if (keyPath.Contains("Oracle", StringComparison.OrdinalIgnoreCase))
        {
            name = "oracle";
            return true;
        }

        return false;
    }

    private static bool TryMapDbPackage(string package, out string tech)
    {
        tech = "Database";
        if (package.Contains("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "SqlServer";
            return true;
        }

        if (package.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            tech = "PostgreSQL";
            return true;
        }

        if (package.Contains("Pomelo.EntityFrameworkCore.MySql", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("MySql.Data", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("MySqlConnector", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MySql";
            return true;
        }

        if (package.Contains("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Sqlite";
            return true;
        }

        if (package.Contains("MongoDB.Driver", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MongoDB";
            return true;
        }

        if (package.Contains("Microsoft.Azure.Cosmos", StringComparison.OrdinalIgnoreCase))
        {
            tech = "CosmosDb";
            return true;
        }

        if (package.Contains("Oracle.ManagedDataAccess", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Oracle";
            return true;
        }

        if (package.Contains("Dapper", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Dapper";
            return true;
        }

        if (package.Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
        {
            tech = "EntityFramework";
            return true;
        }

        return false;
    }

    private static bool TryMapDbInvocation(string methodFullName, out string tech)
    {
        tech = "Database";
        if (methodFullName.Contains("UseSqlServer", StringComparison.OrdinalIgnoreCase))
        {
            tech = "SqlServer";
            return true;
        }
        if (methodFullName.Contains("UseNpgsql", StringComparison.OrdinalIgnoreCase))
        {
            tech = "PostgreSQL";
            return true;
        }
        if (methodFullName.Contains("UseMySql", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MySql";
            return true;
        }
        if (methodFullName.Contains("UseSqlite", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Sqlite";
            return true;
        }
        if (methodFullName.Contains("AddDbContext", StringComparison.OrdinalIgnoreCase))
        {
            tech = "EntityFramework";
            return true;
        }
        if (methodFullName.Contains("MongoClient.GetDatabase", StringComparison.OrdinalIgnoreCase) ||
            methodFullName.Contains("IMongoClient.GetDatabase", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MongoDB";
            return true;
        }

        if (methodFullName.Contains("Dapper", StringComparison.OrdinalIgnoreCase) &&
            (methodFullName.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
             methodFullName.Contains("Execute", StringComparison.OrdinalIgnoreCase)))
        {
            tech = "Dapper";
            return true;
        }

        return false;
    }

    private static bool TryMapDbType(string typeFullName, out string tech)
    {
        tech = "Database";
        if (typeFullName.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
        {
            tech = "EntityFramework";
            return true;
        }
        if (typeFullName.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "SqlServer";
            return true;
        }
        if (typeFullName.Contains("NpgsqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "PostgreSQL";
            return true;
        }
        if (typeFullName.Contains("MySqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MySql";
            return true;
        }
        if (typeFullName.Contains("SqliteConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Sqlite";
            return true;
        }
        if (typeFullName.Contains("MongoDB.Driver.MongoClient", StringComparison.OrdinalIgnoreCase) ||
            typeFullName.Contains("MongoDB.Driver.IMongoClient", StringComparison.OrdinalIgnoreCase) ||
            typeFullName.Contains("MongoDB.Driver.IMongoDatabase", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MongoDB";
            return true;
        }

        if (typeFullName.Contains("Microsoft.Azure.Cosmos.CosmosClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "CosmosDb";
            return true;
        }

        if (typeFullName.Contains("Oracle.ManagedDataAccess.Client.OracleConnection", StringComparison.OrdinalIgnoreCase) ||
            typeFullName.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Oracle";
            return true;
        }

        return false;
    }

    private static bool TryMapDbInvocationArgument(string target, int argIndex, out string tech, out DbInvocationRole role)
    {
        tech = "Database";
        role = DbInvocationRole.Other;
        if (string.IsNullOrWhiteSpace(target)) return false;

        if (target.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "SqlServer";
            role = DbInvocationRole.Endpoint;
            return argIndex == 0;
        }
        if (target.Contains("NpgsqlConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "PostgreSQL";
            role = DbInvocationRole.Endpoint;
            return argIndex == 0;
        }
        if (target.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase))
        {
            tech = "Oracle";
            role = DbInvocationRole.Endpoint;
            return argIndex == 0;
        }
        if (target.Contains("MongoClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "MongoDB";
            role = DbInvocationRole.Endpoint;
            return argIndex == 0;
        }
        if (target.Contains("CosmosClient", StringComparison.OrdinalIgnoreCase))
        {
            tech = "CosmosDb";
            role = argIndex == 0 ? DbInvocationRole.Endpoint : DbInvocationRole.Secret;
            return argIndex == 0 || argIndex == 1;
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
                if (!IsDbKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.EnvVarKey, null, null, key));
            }
            else if (arg.Target.Contains("GetConnectionString", StringComparison.OrdinalIgnoreCase))
            {
                var details = $"ConnectionStrings:{key}";
                if (!IsDbKey(details)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, details));
            }
            else if (arg.Target.Contains("IConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsDbKey(key)) continue;
                AddEvidence(dict, arg.NodeId, new IntegrationEvidence(
                    IntegrationEvidenceKind.ConfigKey, null, null, key));
            }
            else if (arg.Target.Contains("GetSecret", StringComparison.OrdinalIgnoreCase) ||
                     arg.Target.Contains("ISecretProvider", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsDbKey(key)) continue;
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

    private static bool IsDbKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Db", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Sql", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Postgres", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Mongo", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Cosmos", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Oracle", StringComparison.OrdinalIgnoreCase);
    }

    private enum DbInvocationRole
    {
        Other,
        Endpoint,
        Secret
    }
}
