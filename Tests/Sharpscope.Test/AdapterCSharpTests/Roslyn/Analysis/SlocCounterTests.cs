using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Sharpscope.Adapters.CSharp.Roslyn.Analysis;
using Xunit;

namespace Sharpscope.Test.AdapterCSharpTests.Roslyn.Analysis;

public sealed class SlocCounterTests
{
    [Fact(DisplayName = "Counts SLOC excluding blank lines, comments and directives")]
    public void Sloc_BasicSnippet_Works()
    {
        var code = @"
#region X
// comment line

class C
{
    // single-line comment
    void M()
    {
        int x = 0; // inline comment
        /* multi
           line
           comment */
        if (x > 0)
        {
            x++;
        }
    }
}
";
        // Expected lines with real tokens:
        // class C
        // {
        // void M()
        // {
        // int x = 0; // inline
        // if (x > 0)
        // {
        // x++;
        // }
        // }
        // => 8
        SlocCounter.Count(code).ShouldBe(8);
    }

    [Fact(DisplayName = "Empty or whitespace-only returns zero")]
    public void Sloc_Empty_ReturnsZero()
    {
        SlocCounter.Count("").ShouldBe(0);
        SlocCounter.Count("   \r\n \n ").ShouldBe(0);
    }

    [Fact(DisplayName = "Comment-only file returns zero")]
    public void Sloc_CommentOnly_ReturnsZero()
    {
        var code = @"
// only comments
/* also comments */
#region hello
// end
";
        SlocCounter.Count(code).ShouldBe(0);
    }
}
