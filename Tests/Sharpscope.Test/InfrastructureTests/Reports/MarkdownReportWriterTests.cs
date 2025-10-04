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
    public async Task WriteAsync_ProducesHeaderAndCountsSection()
    {
        var writer = new MarkdownReportWriter();
        var tmp = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "reports");
        Directory.CreateDirectory(tmp);
        var file = new FileInfo(Path.Combine(tmp, "out.md"));

        var metrics = MetricsResultStub.Create();
        await writer.WriteAsync(metrics, file, CancellationToken.None);

        var text = await File.ReadAllTextAsync(file.FullName);
        text.ShouldContain("# Sharpscope Report");
        text.ShouldContain("## Counts");
    }

    [Fact]
    public void Format_IsMd()
    {
        new MarkdownReportWriter().Format.ShouldBe("md");
    }
}
