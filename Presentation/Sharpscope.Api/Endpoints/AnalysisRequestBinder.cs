using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Api.Endpoints;

/// <summary>
/// JSON-only binder for POST /analyses/run.
/// It tolerates wrong Content-Type by attempting a manual JSON parse.
/// </summary>
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
        // Defensive: this endpoint is JSON-only
        if (http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            return new AnalysisRequestBinder(null, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));

        // Try to read JSON (even if Content-Type is not application/json)
        var body = await TryReadJsonAsync<AnalyzeSolutionRequest>(http, ct);
        if (body is null)
            return Fail("Invalid or empty JSON body.");

        var hasRepo = !string.IsNullOrWhiteSpace(body.RepoUrl);
        if (!hasRepo)
            return Fail("Provide 'repoUrl'.");

        // Format: query override wins; else body.Options.Format; default json
        var format = !string.IsNullOrWhiteSpace(formatOverride)
            ? FormatUtils.Normalize(formatOverride)
            : FormatUtils.Normalize(body.Options?.Format);

        var dto = new AnalyzeSolutionRequest
        {
            RepoUrl = body.RepoUrl,
            Options = new AnalyzeSolutionOptions
            {
                Format = format,
                Disposition = body.Options?.Disposition
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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // nothing to clean
}
