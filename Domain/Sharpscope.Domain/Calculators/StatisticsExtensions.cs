using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpscope.Domain.Calculators;

/// <summary>
/// Simple statistical helpers (mean, median, standard deviation).
/// All methods are null-safe (throw <see cref="ArgumentNullException"/> on null) and
/// return 0.0 for empty sequences (or n&lt;2 for sample stdev).
/// </summary>
public static class StatisticsExtensions
{
    #region Public API

    public static double Mean(this IEnumerable<int> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var list = source.ToList();
        if (list.Count == 0) return 0.0;
        double sum = 0;
        for (int i = 0; i < list.Count; i++) sum += list[i];
        return sum / list.Count;
    }

    public static double Mean(this IEnumerable<double> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var list = source.ToList();
        if (list.Count == 0) return 0.0;
        double sum = 0;
        for (int i = 0; i < list.Count; i++) sum += list[i];
        return sum / list.Count;
    }

    public static double Median(this IEnumerable<int> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var list = source.ToList();
        if (list.Count == 0) return 0.0;

        list.Sort();
        int n = list.Count;
        if ((n & 1) == 1) return list[n / 2];
        // even: average of middle two
        return (list[n / 2 - 1] + list[n / 2]) / 2.0;
    }

    public static double Median(this IEnumerable<double> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var list = source.ToList();
        if (list.Count == 0) return 0.0;

        list.Sort();
        int n = list.Count;
        if ((n & 1) == 1) return list[n / 2];
        return (list[n / 2 - 1] + list[n / 2]) / 2.0;
    }

    /// <summary>
    /// Population standard deviation (√(Σ (x-μ)² / n)).
    /// Returns 0.0 for empty sequences.
    /// </summary>
    public static double StandardDeviation(this IEnumerable<int> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var values = source.Select(x => (double)x).ToList();
        return PopulationStd(values);
    }

    /// <summary>
    /// Population standard deviation (√(Σ (x-μ)² / n)).
    /// Returns 0.0 for empty sequences.
    /// </summary>
    public static double StandardDeviation(this IEnumerable<double> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var values = source.ToList();
        return PopulationStd(values);
    }

    /// <summary>
    /// Sample standard deviation (√(Σ (x-μ)² / (n-1))).
    /// Returns 0.0 if n &lt;= 1.
    /// </summary>
    public static double SampleStandardDeviation(this IEnumerable<int> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var values = source.Select(x => (double)x).ToList();
        return SampleStd(values);
    }

    /// <summary>
    /// Sample standard deviation (√(Σ (x-μ)² / (n-1))).
    /// Returns 0.0 if n &lt;= 1.
    /// </summary>
    public static double SampleStandardDeviation(this IEnumerable<double> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var values = source.ToList();
        return SampleStd(values);
    }

    #endregion

    #region Helpers

    private static double PopulationStd(IReadOnlyList<double> values)
    {
        int n = values.Count;
        if (n == 0) return 0.0;

        var mean = values.Average();
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            var d = values[i] - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / n);
    }

    private static double SampleStd(IReadOnlyList<double> values)
    {
        int n = values.Count;
        if (n <= 1) return 0.0;

        var mean = values.Average();
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            var d = values[i] - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / (n - 1));
    }

    #endregion
}
