using Microsoft.AspNetCore.Mvc;
using Sharpscope.Application.DTOs;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;

namespace Sharpscope.Api.Endpoints;

public static class AnalysesEndpoints
{
    public static IEndpointRouteBuilder MapAnalysesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analyses");

        // JSON-only: run analysis from repoUrl OR path
        group.MapPost("/run", RunAsync)
             .Accepts<AnalyzeSolutionRequest>("application/json")
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status415UnsupportedMediaType);

        // Multipart-only: upload a ZIP and run analysis
        group.MapPost("/upload", UploadAsync)
             .DisableAntiforgery() // anti-forgery is not needed for API uploads
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status415UnsupportedMediaType);

        return app;
    }

    // POST /analyses/run  (application/json)
    private static async Task<IResult> RunAsync(
        HttpRequest http,
        AnalyzeSolutionUseCase useCase,
        IEnumerable<IReportWriter> reportWriters,
        CancellationToken ct)
    {
        if (IsMultipart(http))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var formatOverride = http.GetFormatOverride();

        await using var bound = await AnalysisRequestBinder.BindAsync(http, formatOverride, ct);
        if (bound.Error is { } err) return err;

        var disposition = DispositionUtils.Resolve(http, bound.Request!.Options);
        var ucReq = RequestMapper.ToUseCase(bound.Request!);

        var snapshot = await useCase.ExecuteAsync(ucReq, ct);
        var writer = ResolveWriter(reportWriters, ucReq.Format);
        var file = CreateTempFile(writer.Format);
        await writer.WriteAsync(snapshot, file, ct);

        return ResultWriter.WriteFile(file, disposition);
    }

    // POST /analyses/upload  (multipart/form-data)
    private static async Task<IResult> UploadAsync(
        IFormFile file,
        [FromForm] UploadMeta meta,
        AnalyzeSolutionUseCase useCase,
        IEnumerable<IReportWriter> reportWriters,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!IsMultipart(http))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var err = ValidateZip(file);
        if (err is not null) return err;

        // Format: query override wins; else meta.Format; default json
        var queryFmt = http.GetFormatOverride();
        var format = FormatUtils.Normalize(!string.IsNullOrWhiteSpace(queryFmt) ? queryFmt : meta.Format);

        await using var ws = await ZipWorkspace.CreateAsync(file, ct);

        var dto = new AnalyzeSolutionRequest
        {
            RepoUrl = null,
            Options = new AnalyzeSolutionOptions
            {
                Format = format,
                Disposition = string.IsNullOrWhiteSpace(meta.Disposition) ? null : meta.Disposition
            }
        };

        var disposition = DispositionUtils.Resolve(http, dto.Options);

        var ucReq = RequestMapper.ToUseCase(dto);
        var snapshot = await useCase.ExecuteAsync(ucReq, ct);
        var writer = ResolveWriter(reportWriters, format);
        var outFile = CreateTempFile(writer.Format);
        await writer.WriteAsync(snapshot, outFile, ct);

        return ResultWriter.WriteFile(outFile, disposition);
    }

    // ---------- helpers (keep endpoint methods short) ----------

    private static bool IsMultipart(HttpRequest http) =>
        http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static IResult? ValidateZip(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest("Form file 'file' (ZIP) is required.");

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        return null;
    }

    private static IReportWriter ResolveWriter(IEnumerable<IReportWriter> writers, string format)
    {
        var writer = writers.FirstOrDefault(w => string.Equals(w.Format, format, StringComparison.OrdinalIgnoreCase));
        if (writer is null)
        {
            var supported = string.Join(", ", writers.Select(w => w.Format));
            throw new NotSupportedException($"Unknown report format '{format}'. Supported: {supported}");
        }
        return writer;
    }

    private static FileInfo CreateTempFile(string format)
    {
        var name = $"sharpscope-report-{Guid.NewGuid():N}.{format.ToLowerInvariant()}";
        var path = Path.Combine(Path.GetTempPath(), name);
        return new FileInfo(path);
    }

    // Form meta (besides the file)
    public sealed class UploadMeta
    {
        /// <summary>Report format: "json" | "csv" | "md" | "sarif". Default: "json".</summary>
        public string? Format { get; init; }

        /// <summary>Response disposition: "inline" or "attachment".</summary>
        public string? Disposition { get; init; }
    }
}
