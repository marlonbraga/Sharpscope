using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Sharpscope.Test.ArchitectureTests;

/// <summary>
/// Enforces the dependency-direction rules from the constitution's Architecture section:
/// Presentation → Application → Domain, Infrastructure → Domain, Domain depends on nothing.
/// The composition root (Sharpscope.Application.DI.ServiceCollectionExtensions) is the only
/// type in Application allowed to reference Infrastructure/Adapters directly.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Sharpscope.Domain.Models.CodeGraph).Assembly,
            typeof(Sharpscope.Application.UseCases.AnalyzeSolutionUseCase).Assembly,
            typeof(Sharpscope.Adapters.CSharp.CSharpLanguageAdapter).Assembly,
            typeof(Sharpscope.Infrastructure.Sources.GitSourceProvider).Assembly)
        .Build();

    [Fact(DisplayName = "Domain must not depend on Application, Infrastructure, or Adapters")]
    public void Domain_MustNotDependOnOuterLayers()
    {
        var rule = Types().That().ResideInNamespaceMatching(@"^Sharpscope\.Domain(\..*)?$")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Sharpscope\.Application(\..*)?$")
                    .Or().ResideInNamespaceMatching(@"^Sharpscope\.Infrastructure(\..*)?$")
                    .Or().ResideInNamespaceMatching(@"^Sharpscope\.Adapters(\..*)?$"))
            .Because("Domain must not reference any other layer (constitution: Architecture)");

        rule.Check(Architecture);
    }

    [Fact(DisplayName = "Application must not depend on Infrastructure or Adapters, except the composition root")]
    public void Application_MustNotDependOnInfrastructure_ExceptCompositionRoot()
    {
        var rule = Types().That().ResideInNamespaceMatching(@"^Sharpscope\.Application(\..*)?$")
            .And().DoNotHaveFullName("Sharpscope.Application.DI.ServiceCollectionExtensions")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Sharpscope\.Infrastructure(\..*)?$")
                    .Or().ResideInNamespaceMatching(@"^Sharpscope\.Adapters(\..*)?$"))
            .Because("only the composition root (AddSharpscope) may reference Infrastructure directly (constitution: Architecture)");

        rule.Check(Architecture);
    }
}
