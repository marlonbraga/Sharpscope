using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Api.Endpoints;

internal sealed class AnalysisRequestBinder : IAsyncDisposable
{
    public AnalyzeSolutionRequest? Request { get; private set; }
    public IResult? Error { get; private set; }
    private readonly IAsyncDisposable? _workspace;

    private AnalysisRequestBinder(AnalyzeSolutionRequest? req, IResult? error, IAsyncDisposable? ws)
    {
        Request = req;
        Error = error;
        _workspace = ws;
    }

    public static async Task<AnalysisRequestBinder> BindAsync(HttpRequest http, string? formatOverride, CancellationToken ct)
    {
        // multipart/form-data -> ZIP
        if (http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            var form = await http.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Fail("Form file 'file' (ZIP) is required.");

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return new AnalysisRequestBinder(null, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType), null);

            var formats = FormatUtils.CollectFormats(formatOverride, form);

            string? outputDir = form["outputDirectory"];
            string? outputName = form["outputFileName"];
            string? disposition = form["disposition"];
            bool? download = bool.TryParse(form["download"], out var d) ? d : (bool?)null;

            var ws = await ZipWorkspace.CreateAsync(file, ct);

            var dto = new AnalyzeSolutionRequest
            {
                Path = ws.ExtractedDirectory.FullName,
                Options = new AnalyzeSolutionOptions
                {
                    Formats = formats,
                    OutputDirectory = string.IsNullOrWhiteSpace(outputDir) ? null : outputDir,
                    OutputFileName = string.IsNullOrWhiteSpace(outputName) ? null : outputName,
                    Disposition = string.IsNullOrWhiteSpace(disposition) ? null : disposition,
                    Download = download
                }
            };

            return new AnalysisRequestBinder(dto, null, ws);
        }

        // JSON (ou “qualquer coisa” que contenha JSON)
        try
        {
            // Se o Content-Type não for JSON, ainda assim tentamos desserializar manualmente
            AnalyzeSolutionRequest? body = null;

            if (http.HasJsonContentType())
            {
                body = await http.ReadFromJsonAsync<AnalyzeSolutionRequest>(cancellationToken: ct);
            }
            else
            {
                using var reader = new StreamReader(http.Body);
                var raw = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(raw))
                    body = JsonSerializer.Deserialize<AnalyzeSolutionRequest>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (body is null) return Fail("Invalid or empty JSON body.");

            // ?format=... sobrepõe
            if (!string.IsNullOrWhiteSpace(formatOverride))
            {
                var f = FormatUtils.Normalize(formatOverride);
                body = new AnalyzeSolutionRequest
                {
                    Path = body.Path,
                    RepoUrl = body.RepoUrl,
                    Options = new AnalyzeSolutionOptions
                    {
                        Formats = new[] { f },
                        OutputDirectory = body.Options.OutputDirectory,
                        OutputFileName = body.Options.OutputFileName,
                        Disposition = body.Options.Disposition,
                        Download = body.Options.Download
                    }
                };
            }
            else
            {
                var normalized = body.Options.Formats.Select(FormatUtils.Normalize).Distinct().ToArray();
                body = new AnalyzeSolutionRequest
                {
                    Path = body.Path,
                    RepoUrl = body.RepoUrl,
                    Options = new AnalyzeSolutionOptions
                    {
                        Formats = normalized,
                        OutputDirectory = body.Options.OutputDirectory,
                        OutputFileName = body.Options.OutputFileName,
                        Disposition = body.Options.Disposition,
                        Download = body.Options.Download
                    }
                };
            }

            return new AnalysisRequestBinder(body, null, null);
        }
        catch (JsonException)
        {
            return Fail("Malformed JSON body.");
        }

        static AnalysisRequestBinder Fail(string message) =>
            new(null, Results.BadRequest(message), null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_workspace is not null)
            await _workspace.DisposeAsync();
    }
}
