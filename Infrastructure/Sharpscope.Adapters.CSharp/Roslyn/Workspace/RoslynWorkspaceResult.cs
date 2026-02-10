using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Sharpscope.Adapters.CSharp.Roslyn.Workspace;

public sealed record ProjectCompilation(
    string ProjectName,
    string? ProjectPath,
    Compilation Compilation
);

public sealed record RoslynWorkspaceResult(
    string RootPath,
    string? SolutionPath,
    IReadOnlyList<ProjectCompilation> Projects
);
