using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Reports;
using Sharpscope.Test.TestUtils;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Reports;

public sealed class CsvReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesHeaderLine()
    {
        var writer = new CsvReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.csv"));

        var metrics = MetricsResultStub.Create();
        await writer.WriteAsync(metrics, file, CancellationToken.None);

        var text = await File.ReadAllTextAsync(file.FullName);
        text.Split('\n')[0].Trim().ShouldBe("Name,Count");
    }

    [Fact]
    public void Format_IsCsv()
    {
        new CsvReportWriter().Format.ShouldBe("csv");
    }
}
