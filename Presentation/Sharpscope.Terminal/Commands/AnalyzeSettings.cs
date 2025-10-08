using System.ComponentModel;
using Spectre.Console.Cli;

namespace Sharpscope.Cli.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description("Local directory to analyze (required if repo is not provided)")]
    public string? Path { get; init; }

    [CommandOption("-r|--repo")]
    [Description("Git repository URL to clone and analyze (required if path is not provided)")]
    public string? Repo { get; init; }

    [CommandOption("-f|--format")]
    [Description("Output format: json|md|csv|sarif (default: json)")]
    public string? Format { get; init; }

    [CommandOption("-o|--out")]
    [Description("Output file (optional). If omitted, the report will NOT be persisted to disk.")]
    public string? OutputPath { get; init; }

    [CommandOption("-t|--print")]
    [Description("Print result to console: true|false (default: false)")]
    public string? Print { get; init; }
}
