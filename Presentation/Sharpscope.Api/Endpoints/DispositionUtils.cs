using Microsoft.AspNetCore.Http;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Api.Endpoints;

internal static class DispositionUtils
{
    public static string Resolve(HttpRequest http, AnalyzeSolutionOptions? opts)
    {
        // 1) Query string tem prioridade
        var q = (http.Query["disposition"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
        if (q is "inline" or "attachment") return q;

        // 2) Depois Options.Disposition
        var o = (opts?.Disposition ?? string.Empty).Trim().ToLowerInvariant();
        if (o is "inline" or "attachment") return o;

        // 3) Depois Options.Download
        if (opts?.Download is true) return "attachment";
        if (opts?.Download is false) return "inline";

        // 4) Default
        return "inline";
    }
}
