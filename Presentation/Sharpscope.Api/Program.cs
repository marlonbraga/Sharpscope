using Sharpscope.Application.DI;
using Sharpscope.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharpscope(allowMsbuild: true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Endpoints do módulo /analyses
app.MapAnalysesEndpoints();

app.Run();
