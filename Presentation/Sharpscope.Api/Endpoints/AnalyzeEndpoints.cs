using Microsoft.AspNetCore.Mvc;
using Sharpscope.Application.DTOs;
using Sharpscope.Application.UseCases;

namespace Sharpscope.Api.Endpoints;

public static class AnalysesEndpoints
{
    public static IEndpointRouteBuilder MapAnalysesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analyses");

        group.MapPost("/run", RunAsync)
             .Accepts<AnalyzeSolutionRequest>("application/json")
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status415UnsupportedMediaType);

        group.MapPost("/upload", UploadAsync)
             .DisableAntiforgery()
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status415UnsupportedMediaType);

        return app;
    }

    private static async Task<IResult> RunAsync(HttpRequest http, AnalyzeSolutionUseCase useCase, CancellationToken ct)
    {
        if (http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var formatOverride = http.GetFormatOverride();

        await using var bound = await AnalysisRequestBinder.BindAsync(http, formatOverride, ct);
        if (bound.Error is { } err) return err;

        var disposition = DispositionUtils.Resolve(http, bound.Request!.Options);
        var ucReq = RequestMapper.ToUseCase(bound.Request!);

        var file = await useCase.ExecuteAsync(ucReq, ct);
        return ResultWriter.WriteFile(file, disposition);
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        [FromForm] UploadMeta meta,
        AnalyzeSolutionUseCase useCase,
        HttpRequest http,
        CancellationToken ct)
    {
        if (!(http.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ?? false))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (file is null || file.Length == 0)
            return Results.BadRequest("Form file 'file' (ZIP) is required.");

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var collected = new List<string>();

        var qFmt = http.GetFormatOverride();
        if (!string.IsNullOrWhiteSpace(qFmt))
            collected.Add(FormatUtils.Normalize(qFmt));

        if (!string.IsNullOrWhiteSpace(meta.Format))
            collected.Add(FormatUtils.Normalize(meta.Format));

        if (meta.Formats is not null && meta.Formats.Length > 0)
        {
            foreach (var ff in meta.Formats)
            {
                if (string.IsNullOrWhiteSpace(ff)) continue;
                foreach (var part in ff.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    collected.Add(FormatUtils.Normalize(part));
            }
        }

        if (collected.Count == 0) collected.Add("json");
        var distinctFormats = collected.Distinct().ToArray();

        await using var ws = await ZipWorkspace.CreateAsync(file, ct);

        var dto = new AnalyzeSolutionRequest
        {
            Path = ws.ExtractedDirectory.FullName,
            RepoUrl = null,
            Options = new AnalyzeSolutionOptions
            {
                Formats = distinctFormats,
                OutputDirectory = string.IsNullOrWhiteSpace(meta.OutputDirectory) ? null : meta.OutputDirectory,
                OutputFileName = string.IsNullOrWhiteSpace(meta.OutputFileName) ? null : meta.OutputFileName,
                Disposition = string.IsNullOrWhiteSpace(meta.Disposition) ? null : meta.Disposition,
                Download = meta.Download
            }
        };

        var resolvedDisposition = DispositionUtils.Resolve(http, dto.Options);
        var ucReq = RequestMapper.ToUseCase(dto);
        var outFile = await useCase.ExecuteAsync(ucReq, ct);

        return ResultWriter.WriteFile(outFile, resolvedDisposition);
    }

    public sealed class UploadMeta
    {
        public string? Format { get; init; }
        public string[]? Formats { get; init; }
        public string? OutputDirectory { get; init; }
        public string? OutputFileName { get; init; }
        public string? Disposition { get; init; }
        public bool? Download { get; init; }
    }
}