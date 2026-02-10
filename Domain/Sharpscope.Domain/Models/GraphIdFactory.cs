using System;

namespace Sharpscope.Domain.Models;

/// <summary>
/// Generates stable graph node ids for projects, namespaces, types and methods.
/// </summary>
public static class GraphIdFactory
{
    public static string CreateSolutionId(string name)
        => $"solution:{Normalize(name)}";

    public static string CreateProjectId(string relativeCsprojPath)
        => $"project:{NormalizePath(relativeCsprojPath)}";

    public static string CreateNamespaceId(string projectId, string @namespace)
    {
        var ns = string.IsNullOrWhiteSpace(@namespace) ? "(global)" : @namespace;
        return $"ns:{projectId}:{Normalize(ns)}";
    }

    public static string CreateTypeId(string projectId, string symbolMetadataName)
        => $"type:{projectId}:{Normalize(symbolMetadataName)}";

    public static string CreateMethodId(string typeId, string methodSignatureStable)
        => $"method:{typeId}:{Normalize(methodSignatureStable)}";

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var norm = path.Replace('\\', '/');
        return Normalize(norm);
    }

    private static string Normalize(string value)
        => value.Trim();

    public static string TrimGlobalPrefix(string value)
        => value.StartsWith("global::", StringComparison.Ordinal) ? value.Substring("global::".Length) : value;
}
