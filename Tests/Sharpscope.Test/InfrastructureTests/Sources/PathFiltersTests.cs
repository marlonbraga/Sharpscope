using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class PathFiltersTests
{
    [Fact(DisplayName = "Default excludes bin/ obj/ .git/ .vs/ node_modules")]
    public void Default_Excludes_CommonFolders()
    {
        var f = PathFilters.Default();

        f.ShouldInclude("src/Program.cs").ShouldBeTrue();
        f.ShouldInclude("src/bin/Debug/file.txt").ShouldBeFalse();
        f.ShouldInclude("src\\obj\\Debug\\file.txt").ShouldBeFalse(); // Windows slashes
        f.ShouldInclude(".git/config").ShouldBeFalse();
        f.ShouldInclude(".vs/slnFile").ShouldBeFalse();
        f.ShouldInclude("web/node_modules/pkg/index.js").ShouldBeFalse();
    }

    [Fact(DisplayName = "Includes restrict files when provided, still honoring excludes")]
    public void Includes_Restrict_Set()
    {
        var includes = new[] { "src/**" };
        var excludes = new[] { "**/bin/**" };

        var f = new PathFilters(includes, excludes);

        f.ShouldInclude("src/app/Program.cs").ShouldBeTrue();
        f.ShouldInclude("tests/app/Program.cs").ShouldBeFalse(); // not in includes
        f.ShouldInclude("src/bin/Debug/file.txt").ShouldBeFalse(); // excluded
    }

    [Fact(DisplayName = "Glob semantics for **, *, ? work as expected")]
    public void Glob_Semantics_Work()
    {
        var f = new PathFilters(includes: new[] { "src/**/File?.cs" });

        f.ShouldInclude("src/File1.cs").ShouldBeTrue();
        f.ShouldInclude("src/sub/File2.cs").ShouldBeTrue();
        f.ShouldInclude("src/sub/deeper/FileX.cs").ShouldBeTrue();
        f.ShouldInclude("src/sub/deeper/File10.cs").ShouldBeFalse(); // ? matches single char
        f.ShouldInclude("src/other.txt").ShouldBeFalse();
    }
}
