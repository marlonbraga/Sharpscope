using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Sharpscope.Cli.Infrastructure;
using Sharpscope.Cli.Commands;
using Sharpscope.Cli.Services;

namespace Sharpscope.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        // External DI from your application layer
        // Keep this single line to avoid coupling here:
        Sharpscope.Application.DI.ServiceCollectionExtensions.AddSharpscope(services, allowMsbuild: false);

        // Local CLI services
        services.AddSingleton<IConsoleInteractor, SpectreConsoleInteractor>();
        services.AddSingleton<IInputNormalizer, InputNormalizer>();
        services.AddSingleton<ILoadingAnimator, LoadingAnimator>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(cfg =>
        {
            cfg.SetApplicationName("Sharpscope");

            cfg.AddCommand<AnalyzeCommand>("analyze")
               .WithDescription("Analyze a local path or a Git repository")
               .WithExample(new[] { "analyze", "--path", @"C:\proj" })
               .WithExample(new[] { "analyze", "--repo", "https://github.com/org/repo" })
               .WithExample(new[] { "analyze", "-p", @"C:\proj", "-f", "json", "-o", "report.json", "--print", "true" });

            cfg.AddCommand<ListFormatsCommand>("formats")
               .WithDescription("List supported output formats");

            cfg.AddCommand<ListLanguagesCommand>("languages")
               .WithDescription("List supported languages");
        });

        // If no args at all -> go to analyze interop flow
        if (args.Length == 0)
            return await app.RunAsync(new[] { "analyze" });

        return await app.RunAsync(args);
    }
}
