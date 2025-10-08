using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Sharpscope.Application.DI;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Sharpscope CLI");
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        //args = ["analyze", "--path", "C:\\projetos\\Sharpscope", "--format", "json", "--out", $"C:\\projetos\\Sharpscope\\docs\\reports\\{timestamp}_diagnostic.json"];
        //args = ["analyze", "--repo", "https://github.com/marlonbraga/Sharpscope", "--format", "json", "--out", $"C:\\projetos\\Sharpscope\\docs\\reports\\{timestamp}_diagnostic.json"];
        
        args = ["analyze", "--path", "C://Users//mbraga//source//repos//GanhoDeCapital", "--format", "json", "--out", $"C:\\projetos\\Sharpscope\\docs\\reports\\{timestamp}_diagnostic.json"];

        var services = new ServiceCollection()
            .AddSharpscope(allowMsbuild: false);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(cfg =>
        {
            cfg.SetApplicationName("Sharpscope");
            cfg.ValidateExamples();

            cfg.AddCommand<AnalyzeCommand>("analyze")
               .WithDescription("Analyze a local path or a public Git repository")
               .WithExample(new[] { "analyze", "--path", @"C:\repo" })
               .WithExample(new[] { "analyze", "--repo", "https://github.com/org/proj", "--format", "md" })
               .WithExample(new[] { "analyze", "--path", @"C:\repo", "--out", "report.csv" });

            cfg.AddCommand<ListFormatsCommand>("formats")
               .WithDescription("List supported output formats");

            cfg.AddCommand<ListLanguagesCommand>("languages")
               .WithDescription("List supported languages");
        });

        return await app.RunAsync(args);
    }
}

/* -------------------------
 * Commands & Settings
 * ------------------------- */

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandOption("--path")]
    [Description("Local directory to analyze")]
    public string? Path { get; init; }

    [CommandOption("--repo")]
    [Description("Public git URL to clone and analyze")]
    public string? Repo { get; init; }

    // Agora sem DefaultValue: se não vier, consideramos CSV em runtime
    [CommandOption("--format")]
    [Description("Output format: json|md|csv|sarif (default: csv when omitted)")]
    public string? Format { get; init; }

    [CommandOption("--out")]
    [Description("Output file (optional). If not set, content is printed to the console.")]
    public string? OutputPath { get; init; }
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    private readonly AnalyzeSolutionUseCase _useCase;
    private static readonly HashSet<string> _knownFormats =
        new(StringComparer.OrdinalIgnoreCase) { "json", "md", "csv", "sarif" };

    public AnalyzeCommand(AnalyzeSolutionUseCase useCase) => _useCase = useCase;

    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        // 1) Regra de default: se não vier format => CSV
        var formatProvided = !string.IsNullOrWhiteSpace(settings.Format);
        var format = formatProvided ? settings.Format!.Trim() : "csv";

        // (opcional) sanity check: cair para csv se vier inválido
        if (!_knownFormats.Contains(format))
            format = "csv";

        var req = new AnalyzeRequest(
            Path: string.IsNullOrWhiteSpace(settings.Path) ? null : settings.Path,
            RepoUrl: string.IsNullOrWhiteSpace(settings.Repo) ? null : settings.Repo,
            Format: format,
            OutputPath: settings.OutputPath
        );

        var ct = CancellationToken.None;
        var file = await _useCase.ExecuteAsync(req, ct);

        // 2) Se NÃO houver --out => imprimir conteúdo na tela
        if (settings.OutputPath is null)
        {
            var content = await File.ReadAllTextAsync(file.FullName, ct);

            // 3) Se também NÃO houver --format (logo usamos CSV implícito) => tabela bonita
            if (!formatProvided && format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                RenderCsvAsTable(content, title: "Sharpscope Report (CSV)");
            }
            else
            {
                // Imprime "como está" para json/md/sarif ou csv com --format explícito
                AnsiConsole.WriteLine(content);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Report generated:[/] {file.FullName}");
        }

        return 0;
    }

    private static void RenderCsvAsTable(string csv, string? title = null)
    {
        // Parsing CSV simples (suporta aspas)
        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Empty CSV.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();

        if (!string.IsNullOrWhiteSpace(title))
            table.Title = new TableTitle($"[bold cyan]{title}[/]");

        // Header
        var headers = rows[0];
        foreach (var h in headers)
            table.AddColumn(new TableColumn($"[white on blue]{EscapeMarkup(h)}[/]").Centered());

        // Rows
        for (int i = 1; i < rows.Count; i++)
        {
            var cells = rows[i].Select(c => EscapeMarkup(c)).ToArray();
            // Garanta número de células = colunas (preenche faltantes)
            if (cells.Length < headers.Length)
                cells = cells.Concat(Enumerable.Repeat(string.Empty, headers.Length - cells.Length)).ToArray();
            table.AddRow(cells);
        }

        // Estética
        table.Caption = new TableTitle($"[grey]Rows: {rows.Count - 1}[/]");
        AnsiConsole.Write(table);
    }

    // Parse CSV básico com suporte a campos entre aspas e vírgula como separador
    private static List<string[]> ParseCsv(string csv)
    {
        var list = new List<string[]>();
        using var sr = new StringReader(csv);
        string? line;
        while ((line = sr.ReadLine()) is not null)
        {
            var fields = ParseCsvLine(line);
            list.Add(fields);
        }
        return list;
    }

    // Parser de linha CSV simples (separator: ',', quote: '"')
    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    // Escapar aspas duplas dentro de campo
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else
            {
                if (ch == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(ch);
                }
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string EscapeMarkup(string value)
        => value?.Replace("[", "[[").Replace("]", "]]") ?? string.Empty;
}

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

/* -------------------------
 * Spectre <-> MS.DI bridge
 * ------------------------- */
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;
    public TypeRegistrar(IServiceCollection builder) => _builder = builder;

    public ITypeResolver Build() => new TypeResolver(_builder.BuildServiceProvider());

    public void Register(Type service, Type implementation)
        => _builder.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation)
        => _builder.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
        => _builder.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly ServiceProvider _provider;
    public TypeResolver(ServiceProvider provider) => _provider = provider;

    public object? Resolve(Type? type) => type == null ? null : _provider.GetService(type);
    public void Dispose() => _provider.Dispose();
}
