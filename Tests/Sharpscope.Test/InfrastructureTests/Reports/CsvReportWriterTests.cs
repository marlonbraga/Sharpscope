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
    public async Task WriteAsync_WritesCsv()
    {
        var writer = new CsvReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.csv"));

        var snapshot = AnalysisSnapshotStub.Create();
        await writer.WriteAsync(snapshot, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("Name,Count");
        text.ShouldContain("Namespaces");
    }

    [Fact]
    public void Format_IsCsv()
    {
        new CsvReportWriter().Format.ShouldBe("csv");
    }
}
