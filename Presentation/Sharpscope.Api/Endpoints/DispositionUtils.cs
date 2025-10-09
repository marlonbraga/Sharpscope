using Microsoft.AspNetCore.Http;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Api.Endpoints;

/// <summary>
/// Decides whether to return inline or as attachment.
/// Query string (?disposition=...) takes precedence over body options.
/// </summary>
internal static class DispositionUtils
{
    public static string Resolve(HttpRequest http, AnalyzeSolutionOptions? opts)
    {
        // 1) Query string has priority
        var q = (http.Query["disposition"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
        if (q is "inline" or "attachment") return q;

        // 2) Then body options
        var o = (opts?.Disposition ?? string.Empty).Trim().ToLowerInvariant();
        if (o is "inline" or "attachment") return o;

        // 3) Default
        return "inline";
    }
}
