namespace Sharpscope.Domain.Models;

/// <summary>
/// Global summary (15 metrics).
/// </summary>
public sealed record SummaryMetrics(
    int TotalNamespaces,
    int TotalTypes,
    double MeanTypesPerNamespace,

    int TotalSloc,
    double AvgSlocPerType,
    double MedianSlocPerType,
    double StdDevSlocPerType,

    int TotalMethods,
    double AvgMethodsPerType,
    double MedianMethodsPerType,
    double StdDevMethodsPerType,

    int TotalComplexity,
    double AvgComplexityPerType,
    double MedianComplexityPerType,
    double StdDevComplexityPerType
);
