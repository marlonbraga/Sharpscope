using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Models;

namespace Sharpscope.Adapters.CSharp;

/// <summary>
/// ILanguageAdapter for C# using Roslyn.
/// </summary>
public sealed class CSharpLanguageAdapter : ILanguageAdapter
{
    #region Fields & Ctor

    private readonly RoslynWorkspaceLoader _loader;
    private readonly CodeGraphBuilder _builder;

    public CSharpLanguageAdapter(RoslynWorkspaceLoader loader, CodeGraphBuilder builder)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    #endregion

    #region ILanguageAdapter

    public string LanguageId => "csharp";

    public bool CanHandle(string languageId) =>
        string.Equals(languageId, "csharp", StringComparison.OrdinalIgnoreCase);

    public async Task<CodeGraph> BuildGraphAsync(DirectoryInfo workdir, CancellationToken ct)
    {
        if (workdir is null) throw new ArgumentNullException(nameof(workdir));
        if (!workdir.Exists) throw new DirectoryNotFoundException(workdir.FullName);

        var workspace = await _loader.LoadWorkspaceAsync(workdir.FullName, ct).ConfigureAwait(false);
        var graph = _builder.Build(workspace, ct);
        return graph;
    }

    #endregion
}
