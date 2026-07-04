# Mapa de Arquitetura — SharpScope

> Documento gerado por análise estrutural da solution. **Não é uma especificação** —
> apenas um retrato do estado atual do código para orientar trabalho futuro.
> Gerado em 2026-07-03.

## Contexto de domínio

SharpScope **não é** uma aplicação CRUD/negócio tradicional. É uma ferramenta de
**análise estática de código C#**: recebe um repositório (local ou git), calcula
métricas de qualidade/acoplamento, detecta integrações de terceiros (bancos de
dados, filas, cache, etc.) usadas pelo código analisado, e produz relatórios
(JSON/Markdown/CSV/SARIF). Isso muda a leitura do restante do documento: não há
"Controllers de domínio de negócio" no sentido clássico (Pedido, Cliente, Pagamento),
e sim endpoints/comandos que disparam um pipeline de análise.

---

## 1. Projetos da solution e responsabilidade aparente

| Projeto | Caminho | Responsabilidade aparente |
|---|---|---|
| **Sharpscope.Domain** | `Domain/Sharpscope.Domain/` | Núcleo de domínio: modelos de código analisado (`ModuleNode`, `NamespaceNode`, `TypeNode`, `MethodNode`), modelos de métricas (`MethodMetrics`, `TypeMetrics`, `NamespaceMetrics`), grafo de dependências e ciclos (`DependencyGraph`, `DependencyCycle`), calculadoras (`MetricsEngine`, `CouplingMetricsCalculator`, etc.) e as interfaces de contrato (`ILanguageAdapter`, `IMetricsEngine`, `IReportWriter`, `ISourceProvider`, `ILanguageDetector`, `IIntegrationDiscoveryEngine`). Não depende de nenhum outro projeto — é a camada mais interna. |
| **Sharpscope.Application** | `Application/Sharpscope.Application/` | Orquestração do caso de uso principal (`AnalyzeSolutionUseCase`): materializar fonte → detectar linguagem → resolver adapter → construir grafo → calcular métricas → descobrir integrações. Define os DTOs de fronteira (`AnalyzeSolutionRequest/Options/Result`) e o registro de DI (`AddSharpscope()`). Pouca regra própria — delega para Domain/Infrastructure. |
| **Sharpscope.Adapters.CSharp** | `Infrastructure/Sharpscope.Adapters.CSharp/` | Implementação de `ILanguageAdapter` específica para C#, usando Roslyn. Contém *walkers* de AST (`CyclomaticComplexityWalker`, `FieldAccessWalker`, `InvocationWalker`, `NestingDepthWalker`), carregamento de workspace (`RoslynWorkspaceLoader`) e construção do modelo intermediário (`CodeGraphBuilder`, `CSharpModelBuilder`). |
| **Sharpscope.Infrastructure** | `Infrastructure/Sharpscope.Infrastructure/` | Serviços de infraestrutura cross-cutting: provedores de fonte (`LocalSourceProvider`, `GitSourceProvider`, `GitOrLocalSourceProvider`), detecção de linguagem (`SimpleExtensionLanguageDetector`), escritores de relatório (`JsonReportWriter`, `MarkdownReportWriter`, `CsvReportWriter`, `SarifReportWriter`) e o motor de descoberta de integrações (`IntegrationDiscoveryEngine` + detectores especializados: `DatabaseDetector`, `HttpClientDetector`, `MessageBusDetector`, `CacheDetector`, `StorageDetector`, `KeyVaultDetector`, `OpenTelemetryDetector`). |
| **Sharpscope.Api** | `Presentation/Sharpscope.Api/` | API REST em ASP.NET Core (Minimal APIs) que expõe a análise via HTTP (`POST /analyses/run`, `POST /analyses/upload`), com fila de jobs em background (`InMemoryJobQueue`, `BackgroundAnalyzerHostedService`) e documentação Swagger. |
| **Sharpscope.Terminal** | `Presentation/Sharpscope.Terminal/` | CLI interativa (Spectre.Console) com comandos `AnalyzeCommand`, `ListFormatsCommand`, `ListLanguagesCommand`. Usa serviços próprios de UI (`SpectreConsoleInteractor`, `InputNormalizer`, `LoadingAnimator`). |

**Fora do escopo de negócio** (apenas suporte a testes, não incluído no grafo abaixo):
- `Tests/Sharpscope.Test` — suíte de testes.
- `Tests/IntegrationFixtures/Samples/*` e `Tests/Sharpscope.Test/Fixtures/*` — projetos-amostra (Sample.SqlServer, Sample.Postgres, Sample.RabbitMq, Sample.Redis, Sample.AzureKeyVault, TinyProject, etc.) usados como *input* para o motor de detecção de integrações ser testado contra código real. Vários desses samples contêm `DbContext` reais (ex.: `SqlServerDbContext`, `PostgresDbContext`) — isso é intencional: são o "código-alvo" que o SharpScope varre, não persistência do próprio SharpScope.

---

## 2. Grafo de dependências entre projetos

```
Sharpscope.Domain                (sem dependências — camada mais interna)
   ↑
   ├── Sharpscope.Infrastructure           (→ Domain)
   │        ↑
   ├── Sharpscope.Adapters.CSharp          (→ Domain, Infrastructure)
   │        ↑
   └── Sharpscope.Application              (→ Domain, Infrastructure, Adapters.CSharp)
            ↑
            ├── Sharpscope.Api             (→ Domain, Application, Infrastructure, Adapters.CSharp)
            └── Sharpscope.Terminal        (→ Application, Infrastructure, Adapters.CSharp)
```

Observações:
- Layering limpo, sem dependências circulares nem "de baixo para cima".
- `Domain` é puro (zero `ProjectReference`), como esperado em Clean/Onion Architecture.
- `Api` e `Terminal` são duas frontends independentes sobre o mesmo `Application`, ambas registrando os mesmos serviços via `AddSharpscope()`.
- `Adapters.CSharp` depende de `Infrastructure` (não o contrário) — vale confirmar se essa dependência é só para tipos utilitários/DI ou se há acoplamento mais forte que valeria revisitar.

---

## 3. Principais "Controllers"/Services por domínio funcional

Não existem `*Controller.cs` tradicionais (MVC) — a API usa **Minimal APIs**, e a CLI usa **Commands** do Spectre.Console. Organizando pelos domínios funcionais reais do sistema:

### Orquestração (caso de uso central)
- [Application/Sharpscope.Application/UseCases/AnalyzeSolutionUseCase.cs](Application/Sharpscope.Application/UseCases/AnalyzeSolutionUseCase.cs) — `ExecuteAsync(AnalyzeRequest, CancellationToken)`. Único ponto de entrada de negócio; injeta `ISourceProvider`, `ILanguageDetector`, `IEnumerable<ILanguageAdapter>`, `IMetricsEngine`, `IIntegrationDiscoveryEngine`.

### Entrada HTTP (Sharpscope.Api)
- [Presentation/Sharpscope.Api/Endpoints/AnalyzeEndpoints.cs](Presentation/Sharpscope.Api/Endpoints/AnalyzeEndpoints.cs)
  - `POST /analyses/run` — JSON body (repoUrl ou path) → executa use case → grava relatório em arquivo temp → retorna com content-disposition.
  - `POST /analyses/upload` — multipart/form-data com ZIP → valida ZIP → descompacta em workspace → executa análise → retorna relatório.

### Entrada CLI (Sharpscope.Terminal)
- [Presentation/Sharpscope.Terminal/Commands/AnalyzeCommand.cs](Presentation/Sharpscope.Terminal/Commands/AnalyzeCommand.cs) — modo interativo ou por flags; normaliza source/format/profile, executa use case com spinner, escreve relatório.
- `ListFormatsCommand`, `ListLanguagesCommand` — comandos de descoberta (formatos e linguagens suportadas).

### Fontes de código (materialização)
- `Infrastructure/Sharpscope.Infrastructure/Sources/LocalSourceProvider.cs`
- `Infrastructure/Sharpscope.Infrastructure/Sources/GitSourceProvider.cs`
- `Infrastructure/Sharpscope.Infrastructure/Sources/GitOrLocalSourceProvider.cs` — decide entre local/git.

### Detecção de linguagem
- `Infrastructure/Sharpscope.Infrastructure/Detection/SimpleExtensionLanguageDetector.cs`

### Geração de relatórios
- `Infrastructure/Sharpscope.Infrastructure/Reports/{Json,Markdown,Csv,Sarif}ReportWriter.cs` — todos implementam `IReportWriter.WriteAsync(AnalysisSnapshot, FileInfo, CancellationToken)`.

### Descoberta de integrações (o "domínio de negócio" mais rico do sistema)
- `Infrastructure/Sharpscope.Infrastructure/Integrations/IntegrationDiscoveryEngine.cs` — orquestra os detectores abaixo, pontua candidatos, deduplica, aplica redação de segredos.
- Detectores especializados (cada um encapsula heurísticas de reconhecimento de um tipo de integração): `DatabaseDetector`, `HttpClientDetector`, `MessageBusDetector`, `CacheDetector`, `StorageDetector`, `KeyVaultDetector`, `OpenTelemetryDetector`.

### Cálculo de métricas (Domain)
- `MetricsEngine`, `CouplingMetricsCalculator`, `DependenciesMetricsCalculator`, `NamespacesMetricsCalculator` e afins em `Domain/Sharpscope.Domain/`.

### Adapter Roslyn
- `RoslynWorkspaceLoader`, `CodeGraphBuilder`, `CSharpModelBuilder`, e os walkers (`CyclomaticComplexityWalker`, `FieldAccessWalker`, `InvocationWalker`, `NestingDepthWalker`) em `Infrastructure/Sharpscope.Adapters.CSharp/`.

---

## 4. Regra de negócio implícita em Data Annotations / EF Fluent API

**Resultado: nenhuma.** Os 6 projetos reais (Domain, Application, Adapters.CSharp,
Infrastructure, Api, Terminal) **não contêm nenhum `DbContext`, Data Annotation
(`[Required]`, `[MaxLength]`, `[ForeignKey]`, `[Key]`, `[Range]`) nem Fluent API do
EF Core** (`modelBuilder.Entity<>`, `.HasKey`, `.HasOne`, `.HasMany`, `.IsRequired`,
`.HasConversion`, etc.). SharpScope não tem camada de persistência própria — os
modelos de domínio são objetos em memória, sem necessidade de mapeamento
relacional.

**Onde EF *aparece* na solution:**
- `Infrastructure/Sharpscope.Infrastructure/Integrations/DatabaseDetector.cs` —
  mas aqui EF é apenas **um dos padrões que o detector reconhece** em código de
  terceiros (nomes de pacotes NuGet, chaves de configuração, tipos usados) para
  inferir que o código analisado usa um banco de dados. Não é persistência do
  próprio SharpScope.
- Projetos de fixture em `Tests/IntegrationFixtures/Samples/Sample.SqlServer/`,
  `Sample.Postgres/`, `Sample.Oracle/` etc. têm `DbContext` reais (ex.:
  `SqlServerDbContext.cs`, `PostgresDbContext.cs`) — são **massa de teste**
  (código-alvo a ser escaneado), não parte do produto.

**Onde a regra de negócio "implícita" realmente mora, então:** já que não há
EF/Data Annotations para esconder regras, o equivalente funcional nesta solution
são as **heurísticas de reconhecimento dentro dos detectores de integração**
(`DatabaseDetector`, `HttpClientDetector`, `MessageBusDetector`, `CacheDetector`,
`StorageDetector`, `KeyVaultDetector`, `OpenTelemetryDetector`) e no
`IntegrationDiscoveryEngine` — thresholds de confiança, listas de pacotes/padrões
reconhecidos, e regras de deduplicação/redação de segredos. Essas regras não são
declarativas (não aparecem como atributos), estão embutidas em código
imperativo — são o ponto mais provável para conter lógica de negócio não-óbvia e
merecem atenção caso uma especificação futura precise formalizar "o que conta
como uma integração detectada".

---

## Observações gerais para uma futura especificação

- A solution segue Clean Architecture com layering consistente; qualquer spec
  futura deveria preservar a direção de dependências acima.
- O "domínio de negócio" real do produto é o motor de métricas + o motor de
  descoberta de integrações — são os dois lugares com maior densidade de regras.
  Os detectores de integração são candidatos naturais a uma especificação
  detalhada (critérios de detecção, confiança, formato de saída).
- Api e Terminal compartilham 100% da lógica de negócio via `Application`; specs
  de comportamento deveriam ser escritas no nível do `AnalyzeSolutionUseCase`
  para cobrir as duas frontends de uma vez.
