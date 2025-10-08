using Microsoft.Extensions.DependencyInjection;
using Sharpscope.Domain.Contracts;
using Spectre.Console.Cli;

namespace Sharpscope.Cli.Commands;

public sealed class ListLanguagesCommand : Command
{
    private readonly IServiceProvider _sp;
    public ListLanguagesCommand(IServiceProvider sp) => _sp = sp;

    public override int Execute(CommandContext context)
    {
        using var scope = _sp.CreateScope();
        var adapters = scope.ServiceProvider.GetServices<ILanguageAdapter>();
        foreach (var l in adapters.Select(a => a.LanguageId).OrderBy(x => x))
            Console.WriteLine(l);
        return 0;
    }
}
