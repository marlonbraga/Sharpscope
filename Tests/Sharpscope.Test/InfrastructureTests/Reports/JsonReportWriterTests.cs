using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Reports;
using Sharpscope.Test.TestUtils;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Reports;

public sealed class JsonReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesIndentedJson_WithSchema()
    {
        var writer = new JsonReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.json"));

        var metrics = MetricsResultStub.Create();
        await writer.WriteAsync(metrics, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("\"schema\"");
        text.ShouldContain("sharpscope/metrics@1");
        text.TrimStart().StartsWith("{").ShouldBeTrue();
    }

    [Fact]
    public void Format_IsJson()
    {
        new JsonReportWriter().Format.ShouldBe("json");
    }
}
