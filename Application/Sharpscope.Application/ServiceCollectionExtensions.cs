using System;
using Microsoft.Extensions.DependencyInjection;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;
using Sharpscope.Domain.Calculators; // onde estiver o MetricsEngine
using Sharpscope.Adapters.CSharp;
using Sharpscope.Adapters.CSharp.Roslyn.Modeling;
using Sharpscope.Adapters.CSharp.Roslyn.Workspace;
using Sharpscope.Infrastructure;
using Sharpscope.Infrastructure.Detection;
using Sharpscope.Infrastructure.Reports;
using Sharpscope.Infrastructure.Sources;

namespace Sharpscope.Application.DI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todos os serviços do Sharpscope (Domain, Infra, Adapters, Application).
    /// </summary>
    /// <param name="allowMsbuild">
    /// Se true, o loader tentará abrir .sln/.csproj via MSBuild (se instalado). 
    /// Caso contrário, faz fallback por diretório (recomendado para testes/CLI).
    /// </param>
    public static IServiceCollection AddSharpscope(
        this IServiceCollection services,
        bool allowMsbuild = false)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // -------------------------
        // Application / UseCases
        // -------------------------
        services.AddTransient<AnalyzeSolutionUseCase>();

        // -------------------------
        // Domain
        // -------------------------
        // Registre aqui sua implementação de IMetricsEngine real
        services.AddTransient<IMetricsEngine, MetricsEngine>(); // ajuste o namespace se necessário

        // -------------------------
        // Infrastructure: Sources (Git / Local / Combinado) + helpers
        // -------------------------
        services.AddSingleton<PathFilters>(_ => PathFilters.Default());
        services.AddTransient<GitCli>();
        services.AddTransient<IProcessRunner, ProcessRunner>();
        services.AddTransient<IGitSourceProvider, GitSourceProvider>();
        services.AddTransient<ILocalSourceProvider, LocalSourceProvider>();
        services.AddTransient<ISourceProvider, GitOrLocalSourceProvider>();
        services.AddTransient(_ => TemporaryDirectory.Create());

        // -------------------------
        // Infrastructure: Detection
        // -------------------------
        services.AddTransient<ILanguageDetector, SimpleExtensionLanguageDetector>();

        // -------------------------
        // Infrastructure: Reports
        // -------------------------
        services.AddTransient<IReportWriter, JsonReportWriter>();
        services.AddTransient<IReportWriter, MarkdownReportWriter>();
        services.AddTransient<IReportWriter, CsvReportWriter>();
        services.AddTransient<IReportWriter, SarifReportWriter>();

        // -------------------------
        // Adapters C#: Roslyn loader + builder + adapter
        // -------------------------
        services.AddTransient(_ => new RoslynWorkspaceLoader(allowMsbuild, _.GetRequiredService<PathFilters>()));
        services.AddTransient<CSharpModelBuilder>();
        services.AddTransient<ILanguageAdapter, CSharpLanguageAdapter>();

        return services;
    }
}
