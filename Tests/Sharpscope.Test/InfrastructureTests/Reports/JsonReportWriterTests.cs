using System.IO;
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
    public async Task WriteAsync_WritesSnapshotJson()
    {
        var writer = new JsonReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.json"));

        var snapshot = AnalysisSnapshotStub.Create();
        await writer.WriteAsync(snapshot, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("\"Metadata\"");
        text.TrimStart().StartsWith("{").ShouldBeTrue();
    }

    [Fact]
    public void Format_IsJson()
    {
        new JsonReportWriter().Format.ShouldBe("json");
    }
}
