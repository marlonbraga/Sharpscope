# Convenções de organização de módulo — Sharpscope

> Este documento é referenciado pela seção **Architecture** da
> [constitution](../.specify/memory/constitution.md). Ele descreve, com exemplos reais,
> a estrutura de pastas que o módulo único do Sharpscope já usa hoje — não é um
> template genérico de DDD. Se um segundo módulo de domínio for criado no futuro, a
> decisão de repetir ou divergir desta estrutura deve ser explícita, não presumida.

## Visão geral

O Sharpscope hoje é um monólito de camada única: um módulo de domínio, dividido nos
projetos `Domain`, `Application`, `Infrastructure` (dois `.csproj`) e `Presentation`
(dois `.csproj`). Cada camada vive em seu próprio `.csproj` e segue a regra de
dependência unidirecional descrita na constitution
(`Presentation → Application → Domain ← Infrastructure`, com a exceção conhecida do
composition root em `Application`).

## Domain (`Domain/Sharpscope.Domain`)

| Pasta | Propósito | Exemplos reais |
|---|---|---|
| `Calculators/` | Lógica de cálculo de métricas, pura, sem I/O. | `CouplingMetricsCalculator`, `TypesMetricsCalculator`, `MetricsEngine` |
| `Contracts/` | Interfaces (`I`-prefixed) que Application/Infrastructure implementam ou consomem — é o ponto de inversão de dependência do domínio. | `IGitSourceProvider`, `ILanguageAdapter`, `IMetricsEngine` |
| `Exceptions/` | Exceções específicas do domínio. | `SharpscopeException` |
| `Models/` | Objetos de dados do domínio (grafos, snapshots, métricas). Não chamamos de "Entities"/"ValueObjects" porque o domínio aqui é analítico (grafo de código), não transacional — não há identidade/ciclo de vida no sentido DDD clássico. | `CodeGraph`, `MetricsSnapshot`, `AnalysisSnapshot` |

**Rationale**: o domínio do Sharpscope modela um grafo de código e métricas derivadas
dele, não entidades de negócio com identidade e regras de invariância no sentido DDD
tradicional. Por isso a nomenclatura `Models`/`Calculators` reflete melhor o que o
código faz do que `Entities`/`ValueObjects` faria — forçar essa nomenclatura genérica
seria documentação que não corresponde à realidade (violaria o Princípio III da
constitution: não documentar o "deveria ser" como se fosse o "é").

## Application (`Application/Sharpscope.Application`)

| Pasta | Propósito | Exemplos reais |
|---|---|---|
| `DTOs/` | Objetos de entrada/configuração que cruzam a fronteira Presentation → Application. Sufixos `Options`/`Request`/`Settings` — nunca `Response` (o caso de uso retorna o DTO/modelo de domínio diretamente). | `AnalyzeSolutionOptions`, `AnalyzeSolutionRequest`, `AnalyzeSolutionResult` |
| `UseCases/` | Orquestração de um fluxo de aplicação — não "Services" genéricos. Cada caso de uso tem uma interface própria. | `AnalyzeSolutionUseCase`, `IAnalyzeSolutionUseCase` |
| `ServiceCollectionExtensions.cs` | Composition root (`AddSharpscope()`). Único ponto de `Application` autorizado a referenciar `Infrastructure` (ver exceção na constitution). | `AddSharpscope(this IServiceCollection services, bool allowMsbuild = false)` |

**Rationale**: "UseCases" em vez de "Services" porque cada classe representa uma ação
completa e nomeada do sistema (ex.: `AnalyzeSolutionUseCase`), não um agrupamento
genérico de métodos relacionados — isso já é a convenção usada e documentada no
Princípio II da constitution.

## Infrastructure (dois projetos: `Sharpscope.Adapters.CSharp` e `Sharpscope.Infrastructure`)

| Pasta | Propósito | Exemplos reais |
|---|---|---|
| `Sources/` | Acesso a código-fonte (Git, disco local). | `GitSourceProvider`, `LocalSourceProvider`, `GitOrLocalSourceProvider` |
| `Detection/` | Detecção de linguagem/tipo de projeto. | `SimpleExtensionLanguageDetector` |
| `Integrations/` | Detectores de padrões de integração (banco, cache, fila, etc.) no código analisado. | `DatabaseDetector`, `CacheDetector`, `MessageBusDetector`, `IntegrationDiscoveryEngine` |
| `Reports/` | Geração de relatórios de saída em formatos diferentes. | `JsonReportWriter`, `MarkdownReportWriter`, `CsvReportWriter`, `SarifReportWriter` |
| `Roslyn/Analysis`, `Roslyn/Modeling`, `Roslyn/Workspace` (em `Sharpscope.Adapters.CSharp`) | Integração com o compilador Roslyn para análise de C#. | `CyclomaticComplexityWalker`, `CSharpModelBuilder`, `RoslynWorkspaceLoader` |

**Rationale**: pastas nomeadas pela responsabilidade técnica concreta (o que a
infraestrutura faz: ler fontes, detectar integrações, escrever relatórios), não por
um padrão genérico de camada de dados — porque hoje não existe camada de dados real
(ver seção **Persistence** da constitution).

## Presentation (`Sharpscope.Api` e `Sharpscope.Terminal`)

| Pasta | Propósito | Exemplos reais |
|---|---|---|
| `Sharpscope.Api/Endpoints/` | Minimal API endpoints — não Controllers MVC. | (endpoints registrados via `MapGet`/`MapPost` em `Endpoints/`) |
| `Sharpscope.Api/DI/`, `Sharpscope.Terminal/DI/` | Bootstrap de composição específico de cada host de apresentação. | `Bootstrap.cs` |
| `Sharpscope.Terminal/Commands/` | Comandos de CLI (Spectre.Console ou similar). | `AnalyzeCommand`, `AnalyzeSettings` |

**Rationale**: "Endpoints" em vez de "Controllers" porque `Sharpscope.Api` usa Minimal
APIs, não MVC — nomear a pasta "Controllers" documentaria algo que não existe no
código.

## Se um segundo módulo de domínio surgir

Este documento descreve apenas o módulo único existente. Antes de criar um segundo
módulo, decida explicitamente (em spec/plan próprios, não por inércia):

1. Ele repete esta estrutura de pastas (`Calculators/Contracts/Exceptions/Models`,
   `DTOs/UseCases`, etc.) ou usa uma nomenclatura diferente porque o domínio é
   genuinamente diferente (ex.: transacional em vez de analítico)?
2. Como os módulos se comunicam — via interface/evento, nunca via `ProjectReference`
   direto entre módulos de domínio (regra já prevista na constitution)?
3. Atualize este documento e a constitution na mesma mudança que introduzir o
   segundo módulo.
