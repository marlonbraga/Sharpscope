using Microsoft.Extensions.DependencyInjection;
using Sharpscope.Domain.Contracts;
using Spectre.Console.Cli;

namespace Sharpscope.Cli.Commands;

public sealed class ListFormatsCommand : Command
{
    private readonly IServiceProvider _sp;
    public ListFormatsCommand(IServiceProvider sp) => _sp = sp;

    public override int Execute(CommandContext context)
    {
        using var scope = _sp.CreateScope();
        var writers = scope.ServiceProvider.GetServices<IReportWriter>();
        foreach (var f in writers.Select(w => w.Format).OrderBy(x => x))
            Console.WriteLine(f);
        return 0;
    }
}
