using Microsoft.AspNetCore.Http;

namespace Sharpscope.Api.Endpoints;

internal static class FormatUtils
{
    public static string Normalize(string? s)
    {
        var f = (s ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(f)) f = "json";
        if (f == "serif") f = "sarif";
        return f;
    }

    public static string[] CollectFormats(string? queryOverride, IFormCollection form)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(queryOverride))
            list.Add(Normalize(queryOverride));

        var single = form["format"].ToString();
        if (!string.IsNullOrWhiteSpace(single))
            list.Add(Normalize(single));

        var multi = form["formats"];
        if (multi.Count > 0)
        {
            foreach (var ff in multi)
            {
                if (string.IsNullOrWhiteSpace(ff)) continue;
                foreach (var part in ff.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    list.Add(Normalize(part));
            }
        }

        if (list.Count == 0) list.Add("json");
        return list.Distinct().ToArray();
    }

    public static string GetDispositionOrDefault(this HttpRequest http)
    {
        var d = (http.Query["disposition"].ToString() ?? "inline").Trim().ToLowerInvariant();
        return (d == "attachment") ? "attachment" : "inline";
    }

    public static string? GetFormatOverride(this HttpRequest http)
    {
        var f = http.Query["format"].ToString();
        return string.IsNullOrWhiteSpace(f) ? null : f;
    }
}
