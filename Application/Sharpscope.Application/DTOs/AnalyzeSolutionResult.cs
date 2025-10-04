using System.Collections.Generic;
using System.IO;
using Sharpscope.Domain.Models;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Use case output: the working directory, computed metrics and report files.
/// </summary>
public sealed class AnalyzeSolutionResult
{
    public AnalyzeSolutionResult(DirectoryInfo WorkDirectory, MetricsResult Metrics, IReadOnlyList<FileInfo> Reports)
    {
        this.WorkDirectory = WorkDirectory;
        this.Metrics = Metrics;
        this.Reports = Reports;
    }

    public DirectoryInfo WorkDirectory { get; }
    public MetricsResult Metrics { get; }
    public IReadOnlyList<FileInfo> Reports { get; }
}