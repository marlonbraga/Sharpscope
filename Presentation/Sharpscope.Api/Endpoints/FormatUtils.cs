using Microsoft.AspNetCore.Http;

namespace Sharpscope.Api.Endpoints;

/// <summary>
/// Format helpers for APIs (kept minimal).
/// </summary>
internal static class FormatUtils
{
    public static string Normalize(string? s)
    {
        var f = (s ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(f)) f = "json";
        if (f == "serif") f = "sarif"; // common typo
        return f;
    }

    public static string? GetFormatOverride(this HttpRequest http)
    {
        var f = http.Query["format"].ToString();
        return string.IsNullOrWhiteSpace(f) ? null : f;
    }
}
