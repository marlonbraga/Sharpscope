using System.IO;
using Sharpscope.Application.DTOs;
using Sharpscope.Application.UseCases;

namespace Sharpscope.Api.Endpoints;

internal static class RequestMapper
{
    public static AnalyzeRequest ToUseCase(AnalyzeSolutionRequest dto)
    {
        // Escolhe um formato (o UseCase atual é single-format).
        var format = FormatUtils.Normalize(dto.Options?.Formats?.FirstOrDefault());

        // Decide extensão pelo formato
        var ext = format switch
        {
            "json" => ".json",
            "md" => ".md",
            "csv" => ".csv",
            "sarif" => ".sarif",
            _ => ".out"
        };

        // Calcula OutputPath quando OutputDirectory/FileName vierem preenchidos.
        // Caso contrário, deixa null para o UseCase decidir.
        string? outputPath = null;

        if (!string.IsNullOrWhiteSpace(dto.Options?.OutputDirectory) ||
            !string.IsNullOrWhiteSpace(dto.Options?.OutputFileName))
        {
            var dir = !string.IsNullOrWhiteSpace(dto.Options?.OutputDirectory)
                ? dto.Options!.OutputDirectory!
                : Path.GetTempPath();

            var baseName = !string.IsNullOrWhiteSpace(dto.Options?.OutputFileName)
                ? dto.Options!.OutputFileName!
                : $"sharpscope_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            // Garante extensão
            if (string.IsNullOrWhiteSpace(Path.GetExtension(baseName)))
                baseName += ext;

            outputPath = Path.Combine(dir, baseName);
        }

        // >>> AQUI está a correção: usar o construtor requerido <<<
        return new AnalyzeRequest(
            Path: dto.Path,
            RepoUrl: dto.RepoUrl,
            Format: format,
            OutputPath: outputPath
        );
    }
}
