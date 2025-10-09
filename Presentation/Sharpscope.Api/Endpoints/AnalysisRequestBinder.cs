using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Api.Endpoints;

internal sealed class AnalysisRequestBinder : IAsyncDisposable
{
    public AnalyzeSolutionRequest? Request { get; private set; }
    public IResult? Error { get; private set; }

    private AnalysisRequestBinder(AnalyzeSolutionRequest? req, IResult? error)
    {
        Request = req;
        Error = error;
    }

    public static async Task<AnalysisRequestBinder> BindAsync(
        HttpRequest http,
        string? formatOverride,
        CancellationToken ct)
    {
        if (http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            return new AnalysisRequestBinder(null, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));

        var body = await TryReadJsonAsync<AnalyzeSolutionRequest>(http, ct);
        if (body is null)
            return Fail("Invalid or empty JSON body.");

        string[] formats = !string.IsNullOrWhiteSpace(formatOverride)
            ? new[] { FormatUtils.Normalize(formatOverride) }
            : (body.Options?.Formats ?? Array.Empty<string>())
                .Select(FormatUtils.Normalize)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .DefaultIfEmpty("json")
                .ToArray();

        var dto = new AnalyzeSolutionRequest
        {
            Path = body.Path,
            RepoUrl = body.RepoUrl,
            Options = new AnalyzeSolutionOptions
            {
                Formats = formats,
                OutputDirectory = body.Options?.OutputDirectory,
                OutputFileName = body.Options?.OutputFileName,
                Disposition = body.Options?.Disposition,
                Download = body.Options?.Download
            }
        };

        return new AnalysisRequestBinder(dto, null);

        static AnalysisRequestBinder Fail(string message) =>
            new(null, Results.BadRequest(message));
    }

    private static async Task<T?> TryReadJsonAsync<T>(HttpRequest http, CancellationToken ct)
    {
        if (http.HasJsonContentType())
            return await http.ReadFromJsonAsync<T>(cancellationToken: ct);

        using var reader = new StreamReader(http.Body);
        var raw = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(raw)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
