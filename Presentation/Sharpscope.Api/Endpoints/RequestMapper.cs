using Sharpscope.Application.DTOs;
using Sharpscope.Application.UseCases;

namespace Sharpscope.Api.Endpoints;

/// <summary>
/// Maps API DTO to the Use Case request.
/// With the simplified API, we only honor a single format and let the UC
/// generate the output filename/path (timestamped).
/// </summary>
internal static class RequestMapper
{
    public static AnalyzeRequest ToUseCase(AnalyzeSolutionRequest dto)
    {
        // Single format; default to json; normalize common typos (e.g., "serif" -> "sarif")
        var format = FormatUtils.Normalize(dto.Options?.Format);

        // No explicit output path anymore. The UseCase generates a timestamped filename.
        string? outputPath = null;

        return new AnalyzeRequest(
            Path: null,
            RepoUrl: dto.RepoUrl,
            Format: format,
            OutputPath: outputPath
        );
    }
}
