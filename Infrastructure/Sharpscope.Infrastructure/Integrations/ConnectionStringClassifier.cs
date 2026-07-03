using System;

namespace Sharpscope.Infrastructure.Integrations;

internal enum ConnectionStringKind
{
    Unknown,
    SqlServer,
    PostgreSQL,
    MySql,
    Sqlite,
    Oracle,
    MongoDB,
    CosmosDb,
    ServiceBus,
    Storage
}

internal static class ConnectionStringClassifier
{
    // Pre-existing legacy debt: cognitive complexity exceeds the 15 allowed by the Code Quality
    // principle (constitution). Suppressed here rather than lowering the gate for everyone;
    // refactor this method (with a characterization test first, per Principle I) the next time
    // it needs to change.
#pragma warning disable S3776
    public static ConnectionStringKind Classify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ConnectionStringKind.Unknown;

        var v = value.Trim().ToLowerInvariant();

        if (v.Contains("endpoint=sb://") || v.Contains("sharedaccesskey="))
            return ConnectionStringKind.ServiceBus;

        if (v.Contains("accountendpoint=") || v.Contains("documents.azure.com"))
            return ConnectionStringKind.CosmosDb;

        if (v.Contains("defaultendpointsprotocol=") && v.Contains("accountkey="))
            return ConnectionStringKind.Storage;

        if (v.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
            return ConnectionStringKind.MongoDB;

        if (v.Contains("host=") || v.Contains("port="))
            return ConnectionStringKind.PostgreSQL;

        if (v.Contains("service name=") || v.Contains("sid=") || v.Contains("oracle"))
            return ConnectionStringKind.Oracle;

        if (v.Contains("server=") || v.Contains("data source="))
            return ConnectionStringKind.SqlServer;

        if (v.Contains("uid=") || v.Contains("user id="))
            return ConnectionStringKind.MySql;

        if (v.Contains("sqlite"))
            return ConnectionStringKind.Sqlite;

        return ConnectionStringKind.Unknown;
    }
#pragma warning restore S3776
}
