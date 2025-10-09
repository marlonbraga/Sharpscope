using System.IO;

namespace Sharpscope.Api.Endpoints;

internal static class ResultWriter
{
    public static IResult WriteFile(FileInfo file, string disposition)
    {
        if (!file.Exists)
            return Results.Problem("Report file not found.", statusCode: 500);

        var contentType = ContentTypeFor(file.Extension);

        if (disposition == "attachment")
            return Results.File(file.FullName, contentType, file.Name);

        var text = File.ReadAllText(file.FullName);
        return Results.Content(text, contentType);
    }

    public static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".json" => "application/json; charset=utf-8",
        ".md" => "text/markdown; charset=utf-8",
        ".csv" => "text/csv; charset=utf-8",
        ".sarif" => "application/sarif+json; charset=utf-8",
        _ => "application/octet-stream"
    };
}
