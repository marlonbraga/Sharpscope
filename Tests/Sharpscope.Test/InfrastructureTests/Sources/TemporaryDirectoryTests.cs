using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class TemporaryDirectoryTests
{
    [Fact(DisplayName = "Create generates an existing directory under the chosen parent")]
    public void Create_CreatesDirectory()
    {
        var parent = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "sharpscope-tests", "work"));
        if (parent.Exists) parent.Delete(true);

        using var tmp = TemporaryDirectory.Create(parent: parent);

        Directory.Exists(tmp.Root.FullName).ShouldBeTrue();
        tmp.Root.FullName.ShouldStartWith(parent.FullName);
        tmp.ToString().ShouldBe(tmp.Root.FullName);
    }

    [Fact(DisplayName = "Dispose deletes the directory when KeepOnDispose is false")]
    public void Dispose_Deletes_WhenKeepFalse()
    {
        DirectoryInfo path;
        using (var tmp = TemporaryDirectory.Create(keepOnDispose: false))
        {
            path = tmp.Root;
            Directory.Exists(path.FullName).ShouldBeTrue();
        }
        Directory.Exists(path.FullName).ShouldBeFalse();
    }

    [Fact(DisplayName = "DisposeAsync deletes the directory when KeepOnDispose is false")]
    public async Task DisposeAsync_Deletes_WhenKeepFalse()
    {
        DirectoryInfo path;
        await using (var tmp = TemporaryDirectory.Create(keepOnDispose: false))
        {
            path = tmp.Root;
            Directory.Exists(path.FullName).ShouldBeTrue();
        }
        Directory.Exists(path.FullName).ShouldBeFalse();
    }

    [Fact(DisplayName = "KeepOnDispose true preserves the directory on dispose")]
    public void KeepOnDispose_PreservesDirectory()
    {
        DirectoryInfo path;
        using (var tmp = TemporaryDirectory.Create(keepOnDispose: true))
        {
            path = tmp.Root;
            Directory.Exists(path.FullName).ShouldBeTrue();
        }
        Directory.Exists(path.FullName).ShouldBeTrue();

        // cleanup
        try { if (Directory.Exists(path.FullName)) Directory.Delete(path.FullName, true); } catch { }
    }
}
