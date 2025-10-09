using System.Collections.Generic;

namespace Sharpscope.Application.DTOs;

/// <summary>
/// Options that control report creation and output.
/// </summary>
public sealed class AnalyzeSolutionOptions
{
    /// <summary>Report formats to generate. Default: ["json"].</summary>
    public string? Format { get; init; }          // "json" (default), "csv", "md", "sarif"

    /// <summary>
    /// Preferred response disposition: "inline" or "attachment".
    /// If both this and query ?disposition are provided, query wins.
    /// </summary>
    public string? Disposition { get; init; }
}
