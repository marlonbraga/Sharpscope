using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Application.DTOs;

namespace Sharpscope.Application.UseCases;

/// <summary>
/// Use case to analyze a repository or local directory and produce metrics and reports.
/// </summary>
public interface IAnalyzeSolutionUseCase
{
    Task<AnalyzeSolutionResult> ExecuteAsync(AnalyzeSolutionRequest request, CancellationToken ct);
}