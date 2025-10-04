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
    public async Task WriteAsync_WritesValidSarifEnvelope()
    {
        var writer = new SarifReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.sarif"));

        var metrics = MetricsResultStub.Create();
        await writer.WriteAsync(metrics, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var json = await File.ReadAllTextAsync(file.FullName);
        json.ShouldContain("\"version\": \"2.1.0\"");
        json.ShouldContain("\"name\": \"Sharpscope\"");
        json.ShouldContain("\"runs\"");
    }

    [Fact]
    public void Format_IsSarif()
    {
        new SarifReportWriter().Format.ShouldBe("sarif");
    }
}
