using Shouldly;
using Sharpscope.Domain.Models;
using Xunit;

namespace Sharpscope.Test.DomainTests;

public sealed class GraphIdFactoryTests
{
    [Fact(DisplayName = "Project ids normalize path separators")]
    public void ProjectId_NormalizesPath()
    {
        var id = GraphIdFactory.CreateProjectId("src\\MyProject\\MyProject.csproj");
        id.ShouldBe("project:src/MyProject/MyProject.csproj");
    }

    [Fact(DisplayName = "Namespace ids are stable and handle global namespace")]
    public void NamespaceId_GlobalNamespace()
    {
        var projectId = GraphIdFactory.CreateProjectId("MyProject.csproj");
        var id = GraphIdFactory.CreateNamespaceId(projectId, "");
        id.ShouldBe($"ns:{projectId}:(global)");
    }

    [Fact(DisplayName = "Method ids are stable for same signature")]
    public void MethodId_Stable()
    {
        var typeId = GraphIdFactory.CreateTypeId("project:test", "N1.A");
        var id1 = GraphIdFactory.CreateMethodId(typeId, "M(System.String):System.Int32");
        var id2 = GraphIdFactory.CreateMethodId(typeId, "M(System.String):System.Int32");
        id1.ShouldBe(id2);
    }
}
