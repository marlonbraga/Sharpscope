using System;
using System.Collections.Generic;
using Shouldly;
using Sharpscope.Domain.Calculators;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class StatisticsExtensionsTests
{
    #region Mean

    [Fact(DisplayName = "Mean over int sequence returns expected value")]
    public void Mean_Ints_Works()
    {
        var data = new[] { 1, 2, 3, 4 };
        data.Mean().ShouldBe(2.5, 1e-9);
    }

    [Fact(DisplayName = "Mean over double sequence returns expected value")]
    public void Mean_Doubles_Works()
    {
        var data = new[] { 2.0, 2.0, 8.0, 8.0 };
        data.Mean().ShouldBe(5.0, 1e-9);
    }

    [Fact(DisplayName = "Mean throws on null sequence")]
    public void Mean_Null_Throws()
    {
        IEnumerable<int>? ints = null;
        Should.Throw<ArgumentNullException>(() => ints!.Mean());
    }

    #endregion

    #region Median

    [Fact(DisplayName = "Median over odd count (ints) returns middle element")]
    public void Median_Ints_Odd_Works()
    {
        var data = new[] { 9, 1, 5 };
        data.Median().ShouldBe(5.0, 1e-9);
    }

    [Fact(DisplayName = "Median over even count (ints) returns average of middle two")]
    public void Median_Ints_Even_Works()
    {
        var data = new[] { 4, 1, 2, 3 };
        data.Median().ShouldBe(2.5, 1e-9);
    }

    [Fact(DisplayName = "Median over doubles returns expected value")]
    public void Median_Doubles_Works()
    {
        var data = new[] { 1.5, 3.5, 2.5, 4.5 };
        data.Median().ShouldBe(3.0, 1e-9);
    }

    [Fact(DisplayName = "Median over empty returns 0.0")]
    public void Median_Empty_ReturnsZero()
    {
        var data = Array.Empty<double>();
        data.Median().ShouldBe(0.0, 1e-12);
    }

    #endregion

    #region Standard Deviation (population)

    [Fact(DisplayName = "Population standard deviation over ints")]
    public void StdDev_Population_Ints_Works()
    {
        // data: {2,4,4,4,5,5,7,9}
        // mean = 5; variance = 4; stdev = 2
        var data = new[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        data.StandardDeviation().ShouldBe(2.0, 1e-9);
    }

    [Fact(DisplayName = "Population standard deviation over doubles")]
    public void StdDev_Population_Doubles_Works()
    {
        var data = new[] { 1.0, 1.0, 1.0, 1.0 };
        data.StandardDeviation().ShouldBe(0.0, 1e-12);
    }

    [Fact(DisplayName = "Population stdev empty returns 0.0")]
    public void StdDev_Population_Empty_ReturnsZero()
    {
        var data = Array.Empty<int>();
        data.StandardDeviation().ShouldBe(0.0, 1e-12);
    }

    #endregion

    #region Standard Deviation (sample)

    [Fact(DisplayName = "Sample standard deviation over ints")]
    public void StdDev_Sample_Ints_Works()
    {
        // data {2,4,4,4,5,5,7,9} -> sample variance = 4*(n/(n-1)) = 4*(8/7) ≈ 4.571428
        // sample stdev ≈ 2.138089935
        var data = new[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        data.SampleStandardDeviation().ShouldBe(2.138089935, 1e-9);
    }

    [Fact(DisplayName = "Sample standard deviation over doubles returns 0 for n<=1")]
    public void StdDev_Sample_SmallN_ReturnsZero()
    {
        new[] { 42.0 }.SampleStandardDeviation().ShouldBe(0.0, 1e-12);
        Array.Empty<double>().SampleStandardDeviation().ShouldBe(0.0, 1e-12);
    }

    #endregion
}
