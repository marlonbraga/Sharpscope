# Feature Specification: Sharpscope.Domain — Regras de Negócio (Documentação Retroativa)

**Feature Branch**: `001-sharpscope-domain-rules`

**Módulo**: `Domain/Sharpscope.Domain`

**Created**: 2026-07-03

**Atualizado em**: 2026-07-04 — cross-check com `docs/metrics.md` + triagem de disposição para itens sem fonte de intenção documentada

**Status**: Draft

**Input**: User description: "Document business rules inferred from Sharpscope.Domain source code (calculators, models, contracts) as retroactive brownfield documentation" — engenharia reversa de regras de negócio a partir do código-fonte de `Sharpscope.Domain`, com apoio de [docs/mapa-arquitetura.md](../../docs/mapa-arquitetura.md) e [docs/metrics.md](../../docs/metrics.md)

> **Nota metodológica** (Constituição do projeto, Princípio III — "Current-vs-Intended Behavior Discipline"): este documento é retroativo, não descreve uma feature nova. Toda regra abaixo é tagueada `[COMPORTAMENTO ATUAL]` implicitamente — reflete o que o código faz **hoje**, verificado por leitura direta do código-fonte, dos testes existentes e da documentação de métricas já publicada. Regras com lógica implícita, sem teste, ou com efeito colateral potencialmente não intencional recebem **Confiança Baixa** e, quando não há nenhuma fonte (doc, comentário ou teste) que confirme a intenção original, uma **Disposição** explícita em vez de uma pergunta em aberto — seguindo o próprio Princípio III/I da Constituição: quando ninguém sabe a intenção original, o padrão é documentar o comportamento atual como verdade provisória e decidir uma disposição (aceitar / adiar / testar), não bloquear o trabalho esperando uma resposta que talvez nunca exista. Nenhum código de produção foi alterado para produzir este documento.

---

## Sumário executivo

Sharpscope.Domain **não tem persistência**: nenhum `DbContext`, Data Annotation (`[Required]`, `[MaxLength]`, `[Key]`, `[ForeignKey]`, `[Range]`, etc.) ou Fluent API do EF Core (`modelBuilder.Entity<>`, `.HasKey`, `.HasOne`, `.IsRequired`, `.HasConversion`, etc.) existe em nenhum arquivo do módulo — confirmado por busca textual exaustiva (ver BR-027). Todos os "modelos" são `record`s imutáveis sem validação declarativa.

O equivalente funcional das "regras escondidas em Data Annotations" neste módulo são os **clamps numéricos, fallbacks silenciosos e exclusões implícitas** espalhados pelas calculadoras. Das 10 regras inicialmente marcadas como ambíguas, **4 foram confirmadas como intencionais** por cross-check com `docs/metrics.md` (a documentação pública de métricas já existente no repo). As **6 restantes** não têm nenhuma fonte documentada de intenção (nem doc, nem comentário, nem histórico de commit granular) — para essas, aplicamos a disposição padrão prevista na Constituição do projeto em vez de deixá-las bloqueando o trabalho.

---

## User Scenarios & Testing *(adaptado — Domain é uma biblioteca interna sem UI, consumida por Application)*

### Cenário 1 - Cálculo de métricas de um grafo de código (Priority: P1)

Application invoca `IMetricsEngine.Compute(CodeGraph)` após o Adapter de linguagem construir o grafo; Domain devolve um `MetricsSnapshot` determinístico e completo, sem tocar disco/rede/tempo.

**Teste independente**: construir um `CodeGraph` sintético e chamar `MetricsEngine.Compute` sem nenhuma outra dependência externa — já coberto por `Tests/Sharpscope.Test/DomainTests/MetricsEngineTests.cs`.

### Cenário 2 - Detecção de ciclos de dependência (Priority: P2)

Dado um grafo com referências circulares entre tipos e/ou namespaces, `DependenciesMetricsCalculator.Compute` retorna cada ciclo como um `DependencyCycle` distinto, separado por escopo ("Type" vs "Namespace").

**Teste independente**: `DependenciesMetricsCalculatorTests.Compute_WithCycles_Works`.

### Cenário 3 - Resolução de formato de relatório (Priority: P3)

Dado um `format` (ex.: `"json"`, `"JSON"`, `"Json"`), `ReportWriterResolver.Resolve` localiza o `IReportWriter` correspondente de forma case-insensitive ou lança `NotSupportedException` listando os formatos suportados.

**Teste independente**: hoje **não há teste automatizado** para este cenário (ver BR-026) — é o gap de cobertura mais concreto encontrado no módulo, e a única regra deste documento com uma ação recomendada em vez de simples aceitação (ver Débito Documentado, abaixo).

### Edge cases já tratados explicitamente no código **e confirmados como intencionais por `docs/metrics.md`**

- Grafo vazio → `SummaryMetrics.Empty` (zeros), sem exceção.
- Tipo com ≤1 método ou ≤1 campo → `Lcom3 = 0.0` por definição matemática, documentada em `docs/metrics.md` §3.10.
- Self-loops (X depende de X) → ignorados na contagem de arestas de acoplamento e de dependência interna — documentado em `docs/metrics.md`, seção "Notes & Edge Cases".
- Namespace vazio/nulo → normalizado para o literal `"(global)"` na geração de ids.

### Edge cases sem fonte documentada de intenção (ver Débito Documentado, ao final)

- O que acontece quando as três fontes de contagem de métodos (grafo, lista de métricas, soma por tipo) discordam de verdade? (BR-019)
- Um `TypeNode.FullName` começando com `.` — dado impossível vindo do Adapter real, ou caso real já observado? (BR-023)
- JSON malformado em atributos do grafo é engolido silenciosamente — mascarando um bug do Adapter? (BR-024)

---

## Business Rules *(regras inferidas, por área — substitui a seção padrão "Functional Requirements" do template, conforme Princípio III da Constituição)*

### A. Métricas de Método — `Calculators/MethodsMetricsCalculator.cs`

- **BR-001** — Confiança **Alta**. `CYCLO = 1 + DecisionPoints` (nunca menor que 1, mesmo com 0 pontos de decisão).
  Fonte: `ComputeCyclomatic(int)`, `Domain/Sharpscope.Domain/Calculators/MethodsMetricsCalculator.cs:66-71`; confirmado em `docs/metrics.md` §4.2.
  Testado: `MethodsMetricsCalculatorTests.Compute_SingleMethod_Works`, `ComputeAll_FromModel_Works`.

- **BR-002** — Confiança **Baixa**. Valores brutos negativos de `Sloc`, `Calls`, `MaxNestingDepth`, `Parameters` e `DecisionPoints` são silenciosamente "clampados" para 0 em vez de lançar exceção ou propagar o valor.
  Fonte: `ClampNonNegative(int)`, usado em `Compute(MethodNode)` (MethodsMetricsCalculator.cs:17-21, 73-74). `docs/metrics.md` não menciona esse tratamento.
  **Disposição: Aceito como comportamento atual (não bloqueante).** Hoje só existe o Adapter C#/Roslyn, que nunca produz negativos — o clamp é defesa contra um dado que ainda não ocorreu na prática. **Gatilho para revisitar**: um novo Language Adapter (não-Roslyn) começar a alimentar valores negativos reais, ou um teste de regressão capturar esse caso.

### B. Métricas de Tipo — `Calculators/TypesMetricsCalculator.cs`

- **BR-003** — Confiança **Alta**. `WMC` = soma, por método do tipo, de `(1 + DecisionPoints)`.
  Fonte: linhas 31, 119; `docs/metrics.md` §3.4 (`WMC = Σ CYCLO(method)`). Testado: `tm.Wmc.ShouldBe(4)` (`TypesMetricsCalculatorTests.cs:66`).

- **BR-004** — Confiança **Alta**. `NPM` conta só métodos com `IsPublic == true`; `NOM` conta todos.
  Fonte: linhas 30-31/118-119; `docs/metrics.md` §3.2/3.3. Testado (linha 65).

- **BR-005** — Confiança **Alta**. `DEP` conta todos os alvos distintos em `TypeNode.DependsOnTypes`, incluindo tipos externos; `IDep`/`FanOut` contam só os alvos internos conhecidos no grafo.
  Fonte: linhas 32-36, 201-207; `docs/metrics.md` §3.5/3.6. Testado (`Dep.ShouldBe(2)` vs `IDep.ShouldBe(1)`, linhas 67-68).

- **BR-006** — Confiança **Alta** *(revisada — antes Baixa)*. `IDep` é sempre numericamente idêntico a `FanOut`. Fonte: `TypesMetricsCalculator.cs:35/123`, `CouplingMetricsCalculator.cs:238-239`.
  **Resolvido por `docs/metrics.md` §3.6/3.8 e §6.2/6.4**: o documento já descreve I-DEP e FAN-OUT com a mesma semântica ("distinct internal dependencies" / "internal out-degree"), sem nunca propor um critério que os diferencie. Não é bug — é uma decisão de modelagem já publicada, ainda que talvez redundante em termos de schema.

- **BR-007** — Confiança **Alta**. LCOM3 (bounded): `1 - (Σμ(a) / (m·n))`; retorna `0.0` quando m≤1 ou n≤1.
  Fonte: `ComputeLcom3` (linhas 217-244); `docs/metrics.md` §3.10. Testado (linhas 131-132, 145-146).

- **BR-008** — Confiança **Alta** *(revisada — antes Baixa)*. O resultado de LCOM3 é limitado a `[0.0, 1.0]`.
  **Resolvido por `docs/metrics.md` §3.10**: "Formula (bounded): ... bounded to `[0, 1]`" — o clamp é parte da fórmula documentada publicamente, não uma defesa reativa não documentada.

- **BR-009** — Confiança **Alta**. `NOA` conta todos os campos, sem distinguir público/privado.
  Fonte: linhas 38/126; `docs/metrics.md` §3.9. Testado (`Noa.ShouldBe(2)` com campo público e privado, linhas 30, 71).

### C. Métricas de Namespace — `Calculators/NamespacesMetricsCalculator.cs`

- **BR-010** — Confiança **Alta**. `NAC` conta só `Class`/`Record` com `IsAbstract == true`; interfaces são explicitamente excluídas.
  Fonte: `CountAbstractClassLike` (linhas 66-73); `docs/metrics.md` §2.2 ("interfaces are not counted for NAC"). Testado (linhas 29-45, 57-71).

### D. Acoplamento — `Calculators/CouplingMetricsCalculator.cs`

- **BR-011** — Confiança **Alta**. Instabilidade `I = CE/(CA+CE)`; se `CA+CE==0`, `I=0.0`.
  Fonte: `ComputeInstability` (linhas 151-155); `docs/metrics.md` §5.3 ("0 if CA + CE = 0"). Testado nos extremos (`CouplingMetricsCalculatorTests.cs:60-68`); caso CA=CE=0 isolado não tem teste dedicado, mas a regra está documentada.

- **BR-012** — Confiança **Alta** *(revisada — antes Baixa)*. Abstractness `A = (#abstratos + interfaces)/(#tipos)` — aqui, **ao contrário do NAC (BR-010), interfaces SÃO contadas**.
  **Resolvido por `docs/metrics.md` §2.2 e §5.4, lado a lado**: o próprio documento define as duas métricas com critérios propositalmente diferentes (NAC segue a definição clássica CK de "classes abstratas"; Abstractness segue a definição de Martin, que inclui interfaces). Divergência intencional e já publicada — não é inconsistência de implementação.

- **BR-013** — Confiança **Alta**. `D = |A + I - 1|`. Fonte: linha 136; `docs/metrics.md` §5.5.

- **BR-014** — Confiança **Alta**. Self-loops são ignorados na construção dos grafos de acoplamento.
  Fonte: comentário "Ignore self-loops" (linha 89), replicado em `BuildTypeOutInternal` (linha 197); `docs/metrics.md`, seção "Notes & Edge Cases".

### E. Dependências e Ciclos — `Calculators/DependenciesMetricsCalculator.cs`

- **BR-015** — Confiança **Alta**. DEP/I-DEP em nível de solução seguem a mesma distinção de BR-005, agregada.
  Fonte: `Compute(CodeGraph)` (linhas 20-29); `docs/metrics.md` §7.1/7.2.

- **BR-016** — Confiança **Alta**. Ciclos via Tarjan (SCC); só SCCs com mais de 1 nó contam.
  Fonte: `FindCycles`/`Tarjan` (linhas 121-199); `docs/metrics.md` §7.3. Testado (`Compute_WithCycles_Works`).

- **BR-017** — Confiança **Alta** *(revisada — antes Baixa)*. Como self-loops já são removidos na normalização, um tipo que só depende de si mesmo nunca gera nenhuma sinalização de "auto-referência" — nem como ciclo, nem de outra forma.
  **Resolvido por `docs/metrics.md`**, seção "Notes & Edge Cases": "Self-loops: ignored in coupling counts (they don't inform cross-entity coupling)" combinado com a definição de Cycles (§7.3, só SCC >1 nó). A regra de negócio documentada é explicitamente "só nos importamos com ciclos entre 2+ entidades distintas" — não é lacuna, é escopo deliberado.

### F. Agregação Estatística — `Calculators/SummaryMetricsAggregator.cs` / `StatisticsExtensions.cs`

- **BR-018** — Confiança **Alta**. `Mean`/`Median`/`StandardDeviation` retornam `0.0` para vazio; `SampleStandardDeviation` retorna `0.0` para n≤1.
  Fonte: `StatisticsExtensions.cs`; `docs/metrics.md`, "Notes & Edge Cases" ("statistics return 0.0 for empty inputs"). Totalmente testado.

- **BR-019** — Confiança **Baixa**. `TotalMethods` é resolvido por uma cadeia de 3 fontes com prioridade fixa: (1) contagem de nós `Method` no grafo, (2) `methods.Count`, (3) soma de `Nom` por tipo — usada só se as duas primeiras indicarem zero.
  Fonte: linhas 42-44. `docs/metrics.md` §1.8 só diz "total number of methods" — não menciona a existência de 3 fontes nem qual prevalece em caso de divergência.
  **Disposição: Aceito como comportamento atual (dívida documentada, não bloqueante).** `MethodCountConsistencyTests` confirma que, em casos reais via Roslyn, as três fontes sempre concordam. **Gatilho para revisitar**: se `SummaryMetricsAggregator` for tocado por outro motivo, ou se um teste de regressão pegar uma divergência real entre as fontes.

### G. Identidade e Normalização — `Models/GraphIdFactory.cs`

- **BR-020** — Confiança **Alta**. Ids compostos por prefixo hierárquico, paths normalizados (`\`→`/`) e `Trim()`. Testado (`GraphIdFactoryTests.cs`).
- **BR-021** — Confiança **Alta**. Namespace vazio/nulo → `"(global)"` no id. Testado (`NamespaceId_GlobalNamespace`).
- **BR-022** — Confiança **Baixa**. `TrimGlobalPrefix` remove `global::` só quando está no início da string (`StartsWith`); sem teste unitário dedicado em Domain.
  **Disposição: Aceito como comportamento atual (não bloqueante).** Suficiente para todo padrão hoje emitido pelo Adapter C#/Roslyn. **Gatilho para revisitar**: um novo Adapter produzir `global::` fora do início da string.

### H. Reconstrução de Modelo (Grafo → Árvore) — `Calculators/CodeGraphModelAdapter.cs`

- **BR-023** — Confiança **Baixa**. `ExtractNamespace(fullName)` retorna string vazia quando o nome não contém `.` ou quando o `.` está na posição 0. Fonte: linhas 223-227. Sem teste dedicado.
  **Disposição: Aceito como comportamento atual (não bloqueante).** Um `FullName` começando com `.` não deveria ocorrer vindo do Adapter C# real hoje. **Gatilho para revisitar**: Roslyn (ou outro adapter) passar a emitir esse padrão em algum cenário real (ex.: tipos genéricos/aninhados com formatação atípica).
- **BR-024** — Confiança **Baixa**. `DeserializeStringList`/`DeserializeBoolList` engolem silenciosamente `JsonException` e retornam lista vazia. Fonte: linhas 186-210. Sem teste para JSON malformado.
  **Disposição: Aceito como comportamento atual (dívida documentada, não bloqueante)** — mesmo arquivo já tem uma exceção de complexidade cognitiva aceita pela Constituição (BR-025) com compromisso de refatoração na próxima alteração; este item deve ser resolvido junto, com teste de caracterização primeiro (Princípio I), no mesmo momento em que `ToCodeModel` for tocado.
- **BR-025** — Não é regra de negócio a esclarecer: `ToCodeModel` já está marcado como débito técnico aceito pela Constituição (`#pragma warning disable S3776`), com refatoração planejada para a próxima alteração (Princípio I).

### I. Resolução de Formato de Relatório — `Contracts/IReportWriterResolver.cs` (`ReportWriterResolver`)

- **BR-026** — Confiança **Baixa**. `Resolve` compara formato de forma case-insensitive e lança `NotSupportedException` listando formatos disponíveis; `Resolve("")`/`Resolve(null)` lança `ArgumentException`. **Nenhum teste automatizado** cobre esta classe.
  **Disposição: AÇÃO RECOMENDADA (não é "se", é "quando").** Diferente dos demais itens desta lista, este não é um caso raro de dado impossível — é lógica de negócio real, hoje sem nenhuma rede de segurança. Antes da próxima alteração que toque resolução de formato de relatório, escrever teste de caracterização cobrindo: case-insensitivity, mensagem de erro e lista de formatos, e o `ArgumentException` para formato vazio/nulo (Princípio I, não-negociável — teste vem antes da mudança, não depois).

### J. Ausência de EF Core / Data Annotations — confirmação explícita

- **BR-027** — Confiança **Alta**. Nenhum arquivo em `Domain/Sharpscope.Domain` usa Data Annotations ou Fluent API do EF Core — confirmado por busca textual exaustiva. Todos os modelos são `record`s imutáveis; toda validação observada é imperativa ou matemática (clamps documentados acima).

---

## Key Entities *(include if feature involves data)*

- **`CodeGraph`**: grafo canônico extraído do código-fonte — `GraphNode`s e `GraphEdge`s. Entrada de `IMetricsEngine.Compute`.
- **`CodeModel`**: árvore (`Codebase` + `DependencyGraph`) reconstruída do `CodeGraph` para as calculadoras.
- **`TypeNode` / `MethodNode` / `NamespaceNode` / `ModuleNode` / `FieldNode`**: IR agnóstica de linguagem.
- **`MetricsSnapshot`**: resultado final indexado por id de nó do grafo.
- **`SummaryMetrics`**: 15 métricas agregadas em nível de solução.
- **`TypeMetrics` / `MethodMetrics` / `NamespaceMetrics`**: métricas por entidade.
- **`TypeCouplingMetrics` / `NamespaceCouplingMetrics`**: métricas de acoplamento.
- **`DependencyMetrics` / `DependencyCycle`**: dependências agregadas e ciclos detectados.
- **`IntegrationsSnapshot` / `IntegrationCandidate`**: modelo de integrações externas (definido em Domain, calculado em Infrastructure).
- **`AnalysisSnapshot` / `AnalysisMetadata`**: objeto raiz de exportação.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Todas as fórmulas de métrica com Confiança Alta (21 de 27 regras, após confirmação via `docs/metrics.md`) continuam fixadas por pelo menos um teste automatizado ou documentadas publicamente — verdadeiro hoje.
- **SC-002**: Os 6 itens sem fonte documentada de intenção (BR-002, 019, 022, 023, 024, 026) seguem a disposição registrada na tabela de Débito Documentado abaixo — nenhum bloqueia trabalho atual.
- **SC-003**: A invariante "zero Data Annotations / zero EF Fluent API em Domain" (BR-027) permanece verdadeira.
- **SC-004**: `ReportWriterResolver` (BR-026) ganha teste de caracterização antes de qualquer alteração que toque seu comportamento — único item com ação obrigatória (não apenas aceite passivo).

---

## Assumptions

- O `CodeGraph` de entrada já foi construído corretamente pelo Adapter de linguagem; Domain não revalida sua integridade referencial.
- "Confiança Alta" significa: nome de método/comentário explícito, teste automatizado, e/ou definição publicada em `docs/metrics.md` — não significa necessariamente que a regra é "a correta" do ponto de vista de negócio, apenas que reflete a intenção documentada dos autores.
- O módulo é puramente síncrono e CPU-bound; não há regras de concorrência, cache ou timeout a documentar.
- Este spec cobre apenas `Domain/Sharpscope.Domain`.

---

## Débito Documentado — itens sem fonte de intenção (triagem final, não são mais perguntas em aberto)

| # | Regra | Disposição | Gatilho para revisitar |
|---|---|---|---|
| BR-002 | Clamp de valor negativo → 0 | Aceito como comportamento atual | Novo Adapter (não-Roslyn) alimentar valores negativos reais |
| BR-019 | Fallback de 3 níveis para `TotalMethods` | Aceito como comportamento atual (dívida documentada) | `SummaryMetricsAggregator` ser tocado, ou teste pegar divergência real entre as 3 fontes |
| BR-022 | `TrimGlobalPrefix` só remove prefixo no início | Aceito como comportamento atual | Novo Adapter produzir `global::` fora do início da string |
| BR-023 | `ExtractNamespace` retorna vazio p/ nome iniciando em "." | Aceito como comportamento atual | Adapter emitir esse padrão em cenário real |
| BR-024 | JSON malformado engolido silenciosamente | Aceito como comportamento atual (dívida documentada, junto com BR-025) | `CodeGraphModelAdapter.ToCodeModel` ser tocado (já é débito técnico marcado — Princípio I) |
| BR-026 | `ReportWriterResolver` sem nenhum teste | **Ação recomendada**: escrever teste de caracterização | Antes da próxima alteração que toque resolução de formato de relatório — não condicional, é a próxima vez que o arquivo mudar |

Nenhum destes 6 itens exige uma decisão adicional agora. Se, no futuro, qualquer um dos gatilhos acima ocorrer, o item volta a ser uma pergunta real — nesse momento, com um caso concreto na mão, será bem mais fácil responder do que hoje, em abstrato.
