using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Reports;
using Sharpscope.Test.TestUtils;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Reports;

public sealed class SarifReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesSarif()
    {
        var writer = new SarifReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.sarif"));

        var snapshot = AnalysisSnapshotStub.Create();
        await writer.WriteAsync(snapshot, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("\"version\": \"2.1.0\"");
    }

    [Fact]
    public void Format_IsSarif()
    {
        new SarifReportWriter().Format.ShouldBe("sarif");
    }
}
