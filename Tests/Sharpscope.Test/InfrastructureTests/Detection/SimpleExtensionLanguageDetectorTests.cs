using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Detection;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Detection;

public sealed class SimpleExtensionLanguageDetectorTests
{
    [Fact]
    public async Task DetectLanguage_ReturnsCSharp_WhenCsFilesPresent()
    {
        var root = CreateTempDir();
        var cs = Path.Combine(root, "src", "Program.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(cs)!);
        await File.WriteAllTextAsync(cs, "class Program {}");

        var det = new SimpleExtensionLanguageDetector();
        var lang = await det.DetectLanguageAsync(new DirectoryInfo(root), CancellationToken.None);

        lang.ShouldBe("csharp");
    }

    [Fact]
    public async Task DetectLanguage_ReturnsNull_WhenAmbiguous()
    {
        var root = CreateTempDir();
        var cs = Path.Combine(root, "src", "Program.cs");
        var py = Path.Combine(root, "src", "script.py");
        Directory.CreateDirectory(Path.GetDirectoryName(cs)!);
        await File.WriteAllTextAsync(cs, "class Program {}");
        await File.WriteAllTextAsync(py, "print('hi')");

        var det = new SimpleExtensionLanguageDetector();
        var lang = await det.DetectLanguageAsync(new DirectoryInfo(root), CancellationToken.None);

        lang.ShouldBeNull();
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sharpscope-tests", "det", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
