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

    public IReadOnlyList<IntegrationCandidate> Detect(IntegrationDiscoveryContext context)
    {
        var candidates = new Dictionary<string, IntegrationCandidateBuilder>(StringComparer.Ordinal);

        foreach (var entry in context.ConfigEntries)
        {
            if (!TryMatchConnectionString(entry.KeyPath, out var name)) continue;

            var tech = InferDbTechnologyFromConnection(entry.Value) ?? "Database";
            var logical = string.IsNullOrWhiteSpace(name) ? "default" : name!;
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
            if (!TryMapDbPackage(pkg.Name, out var tech)) continue;

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
            if (!TryMapDbInvocation(inv.MethodFullName, out var tech)) continue;

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
            if (!TryMapDbType(type.TypeFullName, out var tech)) continue;

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
            if (existing.Technology == "Database" && tech != "Database")
                existing.Technology = tech;
            return existing;
        }

        var logical = "default";
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

        return builder;
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

    private static string? InferDbTechnologyFromConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.ToLowerInvariant();
        if (v.Contains("host=") || v.Contains("username=") || v.Contains("port="))
            return "PostgreSQL";
        if (v.Contains("server=") || v.Contains("data source="))
            return "SqlServer";
        if (v.Contains("uid=") || v.Contains("user id="))
            return "MySql";
        if (v.Contains("sqlite"))
            return "Sqlite";
        return null;
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

        return false;
    }
}
