<!--
Sync Impact Report
==================
Version change: 1.1.0 → 1.1.1
Modified principles: none redefined
Added sections: none (this amendment updates tooling-status facts only)
Removed sections: none
Changes:
  - Code Quality: SonarAnalyzer.CSharp is now actually wired in (Directory.Build.props +
    SonarLint.xml threshold=15 + .editorconfig severity=error for S3776), no longer just
    a stated target. 28 pre-existing legacy methods that exceeded the threshold were
    suppressed individually (#pragma warning disable/restore S3776 with a justification
    comment) rather than lowering the gate; each is a marked, searchable debt item.
  - Architecture: dependency-direction rules are now enforced by automated tests
    (TngTech.ArchUnitNET.xUnit) at Tests/Sharpscope.Test/ArchitectureTests/LayerDependencyTests.cs,
    not just documented.
  - Governance: closing tooling-adoption paragraph updated to reflect that both tools
    were configured with explicit user confirmation on 2026-07-03.
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no changes required
  - .specify/templates/spec-template.md ✅ no changes required
  - .specify/templates/tasks-template.md ✅ no changes required
Follow-up TODOs: none
-->

# Sharpscope Constitution

## Core Principles

### I. Test-First Development (NON-NEGOTIABLE)

All new production code — new features, bug fixes, and behavior-changing refactors —
MUST follow Red-Green-Refactor: write a failing test first, confirm it fails for the
expected reason, implement the minimum code to pass, then refactor with tests green.

Sharpscope is a brownfield codebase. Legacy code without full coverage is allowed to
remain untested at rest — retroactively achieving full legacy coverage is NOT required.
However, the instant legacy code is touched (modified, extended, or refactored) as part
of any change, it MUST first receive a characterization test capturing its pre-change
behavior, followed by a test expressing the new expected behavior. A change to
production code MUST NOT be merged without a corresponding test. The only exemptions are
documentation, configuration files, and purely generated code (e.g. scaffolding output).

Rationale: TDD is the mechanism that lets a brownfield project accumulate a trustworthy
safety net incrementally, one touched file at a time, without demanding an upfront
rewrite of legacy tests that isn't justified by the change at hand.

### II. Established Naming & Structural Conventions (NON-NEGOTIABLE)

New code MUST conform exactly to the conventions already observed in the Sharpscope
codebase. These are documentation of existing practice, not aspirational style — do not
introduce competing conventions:

- **Interfaces**: `I` prefix (e.g. `IGitSourceProvider`, `ILanguageAdapter`).
- **Role suffixes**: use the existing vocabulary for what a class does —
  `Calculator` (metrics), `Adapter` (language adapters), `Detector`, `Engine`,
  `Builder`, `Writer` (report writers), `Loader`, `Walker` (Roslyn syntax walkers).
  Do not invent a parallel suffix (e.g. `Impl`, `Manager`, `Helper`) when an
  established one already fits.
- **DTOs/config**: `Options`, `Request`, `Settings` suffixes as already used in
  `Application/Sharpscope.Application/DTOs` and `Presentation/*/Commands`. Do not add a
  `Response` suffix — the project pattern returns the DTO (e.g. `AnalysisSnapshot`)
  directly.
- **Async methods**: always suffixed `Async` (e.g. `MaterializeFromGitAsync`,
  `ExecuteAsync`), with no exceptions for new asynchronous methods.
- **Namespaces**: PascalCase, mirroring folder structure exactly
  (`Infrastructure/Sharpscope.Infrastructure/Reports/` →
  `Sharpscope.Infrastructure.Reports`), even though the corresponding `.csproj`
  filenames stay lowercase per existing practice.
- **File organization**: one public type per file; file name matches the type name.
- **Class mutability**: Domain and Infrastructure classes are `sealed` by default
  unless a concrete extensibility requirement justifies otherwise.
- **Constructor validation**: required dependencies are null-checked via
  `?? throw new ArgumentNullException(...)`.
- **Dependency injection**: registration extensions follow `AddXxx(this
  IServiceCollection ...)`, mirroring `AddSharpscope()` in
  `Application/Sharpscope.Application/ServiceCollectionExtensions.cs`.
- **Tests**: xUnit + Shouldly (assertions) + NSubstitute (mocking) exclusively — no
  mixing in MSTest/NUnit, FluentAssertions, or Moq. Test class names follow
  `<ClassUnderTest>Tests`; test methods follow `MethodOrScenario_ExpectedResult`. Test
  folders mirror source folders (e.g. `Tests/Sharpscope.Test/DomainTests` mirrors
  `Domain/Sharpscope.Domain`).
- **Global project settings**: `Nullable` and `ImplicitUsings` stay enabled at the
  `Directory.Build.props` level; new projects added to the solution MUST inherit them
  rather than opting out.

Any deviation from an established convention MUST be justified in writing (PR
description or ADR) explaining why the existing pattern does not fit — silent
deviation is not permitted.

Rationale: consistency lets contributors predict where things live and what a class
does from its name alone, which matters more in a brownfield codebase where large
areas are unfamiliar to any single contributor.

### III. Current-vs-Intended Behavior Discipline (Retroactive Documentation)

When writing or updating a spec for an existing feature (`/speckit-specify`,
`/speckit-clarify` applied to brownfield areas), and the code's actual behavior appears
to diverge from its likely original intent or from stakeholder expectations, the spec
process MUST:

1. Document the behavior actually observed in the code as the provisional source of
   truth, explicitly tagged `[COMPORTAMENTO ATUAL]`.
2. Separately flag any suspected divergence as `[AMBIGUIDADE-COMPORTAMENTO]`, paired
   with a concrete clarifying question — never silently "correct" the documented
   behavior to what seems intended, and never assume which one is correct.
3. Never modify production code to match documentation during a task that is purely
   retroactive documentation. Behavior changes require their own spec, explicit user
   approval, and full compliance with Principle I (tests).
4. Route unresolved `[AMBIGUIDADE-COMPORTAMENTO]` items through `/speckit-clarify` for
   resolution with the user before `/speckit-plan` or `/speckit-tasks` are generated —
   do not let planning proceed on an assumed answer.

Rationale: in a brownfield project, the code is the only artifact guaranteed to be
up to date. Conflating "what it does" with "what it should do" during documentation
silently launders undiscussed behavior changes into what looks like a neutral spec.

## Code Quality

- **Maximum cognitive complexity per method: 15**, verified via `SonarAnalyzer.CSharp`
  as part of the build. A method that exceeds this threshold MUST be refactored before
  its PR is considered ready for review — this is a hard gate, not a suggestion.
- **More than 300 lines in a class is a warning signal**, not an automatic block: it
  suggests a possible Single Responsibility Principle (SRP) violation. It is not
  enforced automatically, but when a spec implies a class trending toward that size,
  `/speckit-plan` MUST evaluate it explicitly and either propose a split or record a
  justification in that plan's Complexity Tracking table.
- Every new class/method MUST be run against the project's configured analyzer before
  being considered done.

**Tooling status**: `SonarAnalyzer.CSharp` is wired in via `Directory.Build.props`
(applies to every project except `Tests/IntegrationFixtures/*`, which are synthetic
fixtures and opt out via their own `Directory.Build.props`). The S3776 threshold is set
to 15 in `SonarLint.xml`, and `.editorconfig` sets `dotnet_diagnostic.S3776.severity =
error`, so a violation fails the build — this is now an automated gate, not a
manually-checked one. Pre-existing legacy methods that exceeded the threshold at the
time this gate was turned on are suppressed individually with
`#pragma warning disable/restore S3776` and a comment explaining it's legacy debt to
refactor the next time the method is touched (per Principle I) — the gate itself was
not weakened to accommodate them.

## Persistence

**Current state** (verified against the codebase, not assumed): Sharpscope has no
database and no persistence abstraction in its product code today. The only
`Repository`/`DbContext`-named types anywhere in the repository live under
`Tests/IntegrationFixtures/Samples/*` — these are sample fixture projects used to test
Sharpscope's own integration-discovery engine (which detects these exact patterns in
*other* codebases being analyzed); they are not persistence used by Sharpscope itself.
`IGitSourceProvider` / `ILocalSourceProvider` / `ISourceProvider`
(`Domain/Sharpscope.Domain/Contracts`) read source code from disk or Git — they are
source-code access contracts, not a data-persistence layer.

**Going forward**: any future data access (e.g. caching analysis results, storing
history) MUST be abstracted behind an interface defined in Domain or Application,
following the dependency direction in the Architecture section below, so that
introducing real persistence later does not require rewriting the business logic that
consumes it. This constitution intentionally does NOT select a database technology
(SQL, NoSQL, or a specific provider) — that decision belongs in a feature's `plan.md`,
made when the need is real, not speculated here.

## Architecture

**Current shape**: Sharpscope is a single deploy unit organized in layers — Domain,
Application, Infrastructure (`Sharpscope.Adapters.CSharp` + `Sharpscope.Infrastructure`),
Presentation (`Sharpscope.Api` + `Sharpscope.Terminal`) — each already living in its own
`.csproj`. There is currently **one** domain module; Sharpscope is not yet split into
multiple bounded-context modules with independent project boundaries, and this
constitution does not assert otherwise. If and when a second domain module becomes
genuinely necessary, its boundaries (and whether inter-module communication goes
through interfaces/events rather than direct project references) MUST be decided in
that feature's own spec/plan, not presumed here in advance.

**Dependency direction** (verified against every `.csproj` in the solution):

- `Domain` MUST NOT reference any other layer. This already holds today — the `Domain`
  project has zero `ProjectReference` entries.
- `Presentation` → `Application` → `Domain`, and `Infrastructure` → `Domain`, is the
  binding rule for all new code.
- **Known, accepted exception**: `Application`'s composition root — `AddSharpscope()`
  in `Application/Sharpscope.Application/ServiceCollectionExtensions.cs` — references
  `Sharpscope.Infrastructure` and `Sharpscope.Adapters.CSharp` directly, because that
  file's job is to register their concrete types into the DI container. This is the
  **only** permitted reason for `Application` to depend on `Infrastructure`. Business
  logic inside `Application` (use cases, DTOs) MUST NOT reference Infrastructure types
  directly — only the composition-root registration file may.
- The two Infrastructure projects (`Sharpscope.Adapters.CSharp`,
  `Sharpscope.Infrastructure`) may reference each other; both remain on the
  Infrastructure side of the boundary.

**Tooling status**: the Domain-isolation rule and the Application composition-root
exception above are enforced by automated tests using `TngTech.ArchUnitNET.xUnit` at
[Tests/Sharpscope.Test/ArchitectureTests/LayerDependencyTests.cs](../../Tests/Sharpscope.Test/ArchitectureTests/LayerDependencyTests.cs) —
a build/test-suite failure, not just a documented convention.

### Module organization convention

The current (single) module's folder layout is the binding structure for new code — it
is what Principle II already documents and MUST NOT be replaced with a generic
template that doesn't match the real code:

- `Domain/`: `Calculators/`, `Contracts/`, `Exceptions/`, `Models/`
- `Application/`: `DTOs/`, `UseCases/`
- `Infrastructure/`: `Detection/`, `Integrations/`, `Reports/`, `Sources/` (plus the
  Roslyn-specific `Sharpscope.Adapters.CSharp/Roslyn/...` subtree)
- `Presentation/`: `Sharpscope.Api/Endpoints/` (minimal APIs, not MVC Controllers),
  `Sharpscope.Terminal/Commands/`

Full details, rationale per folder, and examples live in
[docs/convencoes-modulo.md](../../docs/convencoes-modulo.md) — this constitution
references that document rather than duplicating it. If a second domain module is ever
introduced, its folder convention (whether it matches or intentionally diverges from
this one) MUST be decided explicitly, not inherited silently.

## Technology & Tooling Constraints

- Stack: .NET (C#), solution layered as Domain → Application → Infrastructure
  (`Sharpscope.Adapters.CSharp`, `Sharpscope.Infrastructure`) → Presentation
  (`Sharpscope.Api`, `Sharpscope.Terminal`), plus a `ui/` frontend (React + Vite +
  TypeScript, ReactFlow-based) that is out of scope for the C# conventions above but
  still subject to Principle I (test new behavior) and Principle III.
- Test stack is fixed: xUnit, Shouldly, NSubstitute. Introducing an alternate
  framework requires a constitution amendment, not a per-PR decision.
- `Directory.Build.props` is the single source of truth for solution-wide compiler
  settings (`Nullable`, `ImplicitUsings`); do not override these per-project without
  a documented reason.

## Development Workflow & Quality Gates

- Every PR that touches production code MUST include or reference the tests required
  by Principle I; reviewers MUST reject PRs that don't.
- Every PR that introduces a new class/interface/DTO MUST be checked against
  Principle II's naming table; unjustified deviations block merge.
- Every PR MUST keep methods at or below cognitive complexity 15 (Code Quality); once
  `SonarAnalyzer.CSharp` is wired into the build this is enforced automatically, until
  then reviewers check it manually.
- Every PR that introduces data access MUST introduce it behind an interface
  (Persistence) rather than a concrete storage call scattered through business logic.
- Every PR MUST respect the Architecture dependency direction; the only permitted
  `Application` → `Infrastructure` reference is the composition root described there.
- Specs produced for existing (brownfield) features MUST be checked for unresolved
  `[AMBIGUIDADE-COMPORTAMENTO]` markers before `/speckit-plan` runs; `/speckit-clarify`
  is the required path to resolve them.
- `/speckit-plan`'s Constitution Check gate evaluates against all principles and
  sections above; violations must be recorded in that plan's Complexity Tracking table
  with a justification, or the deviation must be removed.

## Governance

This constitution supersedes conflicting team conventions and prior undocumented
practice. All PRs and code reviews MUST verify compliance with the principles above;
any complexity or deviation MUST be explicitly justified in the PR description or an
ADR referenced from it.

Amendment procedure: propose the change via `/speckit-constitution` (or a PR editing
this file directly), state the rationale, and update the version per the policy below.
Approved amendments MUST propagate to `.specify/templates/plan-template.md`,
`.specify/templates/spec-template.md`, and `.specify/templates/tasks-template.md` in
the same change if those templates reference the modified principle.

Versioning policy (semantic versioning applied to governance):
- **MAJOR**: backward-incompatible principle removal or redefinition (e.g. dropping
  TDD as non-negotiable, replacing the fixed test stack).
- **MINOR**: a new principle or materially expanded section is added.
- **PATCH**: wording clarifications, typo fixes, non-semantic refinements.

Tooling adoption (SonarAnalyzer.CSharp for Code Quality, TngTech.ArchUnitNET.xUnit for
Architecture's dependency-direction rule) was carried out on 2026-07-03 with explicit
user confirmation, after both tools were shown to build and pass cleanly against the
full solution. Any further tightening of these gates (e.g. removing an existing
`#pragma` suppression, adding new automated rules) still requires the same PR-level
justification as any other constitutional change.

**Version**: 1.1.1 | **Ratified**: 2026-07-02 | **Last Amended**: 2026-07-03
