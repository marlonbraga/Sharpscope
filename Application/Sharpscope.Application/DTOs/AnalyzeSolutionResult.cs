using System.Collections.Generic;
using System.IO;
using Sharpscope.Domain.Models;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Use case output: the working directory, computed metrics and report files.
/// </summary>
public sealed class AnalyzeSolutionResult
{
    public AnalyzeSolutionResult(DirectoryInfo workDirectory, MetricsResult metrics, IReadOnlyList<FileInfo> reports)
    {
        WorkDirectory = workDirectory;
        Metrics = metrics;
        Reports = reports;
    }

    public DirectoryInfo WorkDirectory { get; }
    public MetricsResult Metrics { get; }
    public IReadOnlyList<FileInfo> Reports { get; }
}
