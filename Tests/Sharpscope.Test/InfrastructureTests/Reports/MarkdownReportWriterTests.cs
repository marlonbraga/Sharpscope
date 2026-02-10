using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Reports;
using Sharpscope.Test.TestUtils;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Reports;

public sealed class MarkdownReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesMarkdown()
    {
        var writer = new MarkdownReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.md"));

        var snapshot = AnalysisSnapshotStub.Create();
        await writer.WriteAsync(snapshot, file, CancellationToken.None);

        File.Exists(file.FullName).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("# Sharpscope Report");
        text.ShouldContain("## Summary");
        text.ShouldContain("## Counts");
    }

    [Fact]
    public void Format_IsMarkdown()
    {
        new MarkdownReportWriter().Format.ShouldBe("md");
    }
}
