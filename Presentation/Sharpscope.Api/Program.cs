using Sharpscope.Application.DI;
using Sharpscope.Application.UseCases;
using Sharpscope.Domain.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharpscope(allowMsbuild: true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/formats", (IEnumerable<IReportWriter> writers) =>
    Results.Ok(new { formats = writers.Select(w => w.Format).OrderBy(x => x) }));

app.MapGet("/languages", (IEnumerable<ILanguageAdapter> adapters) =>
    Results.Ok(new { languages = adapters.Select(a => a.LanguageId).OrderBy(x => x) }));

app.MapPost("/analyze", async (AnalyzeRequest req, AnalyzeSolutionUseCase useCase, CancellationToken ct) =>
{
    var file = await useCase.ExecuteAsync(req, ct);

    var contentType = req.Format.ToLowerInvariant() switch
    {
        "json" => "application/json",
        "md" => "text/markdown; charset=utf-8",
        "csv" => "text/csv; charset=utf-8",
        "sarif" => "application/sarif+json",
        _ => "application/octet-stream"
    };

    return Results.File(file.FullName, contentType, Path.GetFileName(file.FullName));
});

app.Run();