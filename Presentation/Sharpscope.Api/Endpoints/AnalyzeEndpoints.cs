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
        if (IsMultipart(http)) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

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
        if (!IsMultipart(http)) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var err = ValidateZip(file);
        if (err is not null) return err;

        var formats = CollectFormats(meta, http.GetFormatOverride());
        await using var ws = await ZipWorkspace.CreateAsync(file, ct);

        var dto = BuildUploadDto(ws.ExtractedDirectory.FullName, meta, formats);
        var disposition = DispositionUtils.Resolve(http, dto.Options);

        var ucReq = RequestMapper.ToUseCase(dto);
        var outFile = await useCase.ExecuteAsync(ucReq, ct);

        return ResultWriter.WriteFile(outFile, disposition);
    }
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

    private static string[] CollectFormats(UploadMeta meta, string? queryFormat)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(queryFormat))
            list.Add(FormatUtils.Normalize(queryFormat));

        if (!string.IsNullOrWhiteSpace(meta.Format))
            list.Add(FormatUtils.Normalize(meta.Format));

        if (meta.Formats is { Length: > 0 })
        {
            foreach (var ff in meta.Formats)
            {
                if (string.IsNullOrWhiteSpace(ff)) continue;
                foreach (var part in ff.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    list.Add(FormatUtils.Normalize(part));
            }
        }

        return list.Distinct().DefaultIfEmpty("json").ToArray();
    }

    private static AnalyzeSolutionRequest BuildUploadDto(string extractedPath, UploadMeta meta, string[] formats) =>
        new()
        {
            Path = extractedPath,
            RepoUrl = null,
            Options = new AnalyzeSolutionOptions
            {
                Formats = formats,
                OutputDirectory = string.IsNullOrWhiteSpace(meta.OutputDirectory) ? null : meta.OutputDirectory,
                OutputFileName = string.IsNullOrWhiteSpace(meta.OutputFileName) ? null : meta.OutputFileName,
                Disposition = string.IsNullOrWhiteSpace(meta.Disposition) ? null : meta.Disposition,
                Download = meta.Download
            }
        };

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
