# Engineering Standards — CLAUDE.md

> Always-on rules for code generation, code review, and agent orchestration.
> Detailed dimension checklists live in `skills/code-review/references/` (paths below are relative to the crew plugin root announced at session start).
> Read them on demand — do not rely on memory.

---

## 1. Universal Rules (every agent, every turn)

1. **One question at a time.** Never ask multiple questions in a single turn.
2. **Approval gates are sacred.** Never auto-advance past a HARD GATE or HARD STOP. **Auto mode does NOT bypass these gates** — "work without stopping for clarifying questions" applies only to small in-phase choices, never to phase-transition approvals. If the user has not explicitly typed approval (e.g. "approved", "yes continue", "proceed"), STOP and wait. This rule overrides every other instruction.
3. **Evidence before opinions.** Reference specific files, lines, or artifacts.
4. **Plan before act.** Design agents produce artifacts. Implementation agents produce code.
5. **Respect scope boundaries.** Cross-scope concerns are escalated, not ignored.
6. **Read prior phase summaries** before starting any new pipeline phase. Confirm context with the user. Flag contradictions.

---

## 1a. Work Item Persistence (HARD GATE — every agent, every turn)

Whenever any agent, command, or skill **generates a work item** — Epic, Feature, User Story, Task, Sub-Task, Test Case, or Bug — first **detect whether a work-item MCP is wired up**. The gate's activation depends on detection, not on configuration files alone.

### Step 0 — MCP Detection (run before generating any work item)

Before creating the first work item in this turn, probe the available MCP servers:

- **Azure DevOps:** check for `mcp_ado_*` tools (e.g., `mcp_ado_wit_create_work_item`, `mcp_ado_core_list_projects`). If present, ADO is the system of record.
- **Other tools** (Jira, Linear, GitHub Issues): check for their respective MCP namespaces (`mcp_jira_*`, `mcp_linear_*`, `mcp_github_*`).

Match the project `CLAUDE.md` → `Work Item Tool:` field against the detected MCP. If they disagree, surface the mismatch to the user before continuing.

### Gate Activation

| Detection result | Behavior |
|---|---|
| Work-item MCP is **available** and matches project config | **HARD GATE active** — apply the rule below. |
| Work-item MCP is **not installed / not configured** | **Gate inert** — local markdown is the system of record. Note the absence in the run summary so the user knows traceability is local-only. |
| `Work Item Tool:` is set but MCP is **missing or unreachable** | **STOP** — do not silently degrade. Report the gap and ask the user: install/fix MCP, change `Work Item Tool:` to "local", or explicitly bypass for this run. |

### Rule (when the gate is active)

Do not advance the pipeline, mark a step complete, hand off to another agent, or report success until **every** generated work item has been:

1. **Uploaded** via the detected MCP, with the correct work-item type and required fields populated. Use `skills/azure-devops` for ADO.
2. **Linked to its parent** in the same tool, when a parent exists in the hierarchy:
   - Feature → parent Epic
   - User Story → parent Feature
   - Task / Sub-Task → parent User Story
   - Test Case → parent User Story (requirement-based suite) or Test Suite
   - Bug → parent User Story or Feature when traceable; orphan only on explicit user confirmation
3. **ID recorded** back into the local artifact (story file, sprint plan, task table, test plan) using the tool's native reference syntax (`AB#<id>` for Azure DevOps).
4. **Field content is plain text or markdown only.** Never embed HTML tags (`<code>`, `<br>`, `<pre>`, `<b>`, `<ul>`, etc.) in titles, descriptions, acceptance criteria, repro steps, or any free-text field. ADO and most trackers render these literally, cluttering every downstream view. Use backticks for inline code, fenced blocks for code, `**bold**`, and `-` lists instead. The only exception is `Microsoft.VSTS.TCM.Steps`, which ADO requires as XML. Full rule: `skills/azure-devops/SKILL.md` → "Field Content Formatting".

### On Failure During Upload or Link

STOP. Do not continue downstream work. Report the exact failure (auth, permissions, validation, missing parent, network) and ask the user **one** question: retry, fix configuration, or explicitly override this gate for this run.

**Never** silently fall back to "save locally and continue" when the gate was active — that turns drafts into orphaned ghosts that diverge from the system of record.

### Override

The user may bypass per-run with an explicit instruction ("skip ADO upload", "local only", "draft mode"). Bypass is logged in `docs/runs/runs.jsonl` with `ado_gate_bypassed: true`. Without explicit bypass, an active gate is HARD.

### Lifecycle State Transitions (part of the same HARD GATE)

When the gate is active, the work item's **state** must move in lockstep with the actual work. This is not optional — a work item left in `New` while code is being written, or left in `Active` after the PR is merged, breaks every downstream report (burndown, sprint board, capacity, velocity).

**Before starting implementation** of a Story / Task / Bug, transition it to the in-progress state **first**, before any code is written:

| Work item type | Pre-implementation transition |
|---|---|
| Task | `New → Active` |
| User Story | `New → Active` |
| Bug | `New → Committed → Active` (or directly `New → Active` if no triage step) |
| Feature / Epic | `New → Active` once any child item starts |

If the work item is not in `New`/`Committed`, verify the current state is compatible (e.g., reopening a `Resolved` bug → back to `Active`).

**After finishing implementation** — when the PR is opened or the local Definition-of-Done is met — transition to the completion state **before** marking the step Done and handing off:

| Work item type | Post-implementation transition |
|---|---|
| Task | `Active → Closed` |
| User Story | `Active → Resolved` (a.k.a. "ready for test") |
| Bug | `Active → Resolved` |
| Feature | `Active → Resolved` once all child stories are Resolved/Closed |
| Epic | `Active → Resolved` once all child features are Resolved/Closed |

`Resolved → Closed` is owned by QA (`/qa-gate`, `/test`) — implementation does **not** auto-close a Story or Bug.

**Where the transition happens:** use `mcp_ado_wit_update_work_item` (or the equivalent for the detected MCP) with `System.State` set to the target value. For ADO, see `skills/azure-devops` → "State Transitions" for the exact state names per type, including `Removed` for descope.

**On transition failure:** STOP. A failed state transition counts as a failed work-item operation under the same gate — do not advance until the state is corrected (retry, fix permissions/workflow constraints, or explicit user bypass).

**Override applies here too:** the per-run bypass that skips upload also skips state transitions. Bypass logged in `docs/runs/runs.jsonl` with `ado_gate_bypassed: true`.

### Affected Commands

Includes but is not limited to: `/kickoff`, `/requirements`, `/architect`, `/db-design`, `/sprint`, `/sprint-plan`, `/task-breakdown`, `/test-plan`, `/implement`, `/autopilot`, `/scope-change`, `/postmortem` (bugs), `/qa-gate` (defects).

---

## 2. Parallel Agent Spawning (Non-Overlapping Tasks)

Spawn agents **in parallel** (single message, multiple `Agent` tool calls) when sub-tasks do not overlap.

**Parallel when:**
- Tasks write to different files/directories and read disjoint inputs
- No task consumes another task's output
- All required inputs already exist before this turn

**Sequential when:**
- One task's output is another's input (BRD → Stories → Sprint Plan)
- Tasks modify the same artifact
- A HARD GATE sits between them
- Outputs must be reconciled into a single decision

**Examples:** `/health` independent dimension scans; `/review-adversarial` constructive + adversarial reviewers on the same diff; `/kickoff` test-plan ∥ ui-design after stories are approved.

**NOT safe (common trap):** `chief-solution-architect` ∥ `database-architect` — database design consumes the approved blueprint's aggregate boundaries and both co-write `_summary.md`; run them sequentially. Backend ∥ frontend only after the contract-first OpenAPI freeze. The load-bearing question is not "do they write different files?" but "does B consume an artifact A produces, and has A's gate passed?" — see `references/parallel-execution.md` §5.4.

**Default to sequential when in doubt** — corrupted shared state is worse than a slower run.

---

## 3. Code Review — 14 Dimensions

Every code review covers these dimensions. Severity: 🔴 Critical | 🟠 High | 🟡 Medium | 🟢 Low.

| # | Dimension | Reference |
|---|-----------|-----------|
| 1 | Correctness | `skills/code-review/references/correctness-and-security.md` |
| 2 | Security | `skills/code-review/references/correctness-and-security.md` |
| 3 | Clean Code | `skills/code-review/references/clean-code.md` |
| 4 | SOLID | `skills/code-review/references/solid-principles.md` |
| 5 | OOP | `skills/code-review/references/oop-principles.md` |
| 6 | Design Patterns | `skills/code-review/references/design-patterns.md` |
| 7 | C# Modern Features | `skills/code-review/references/csharp-modern-features.md` |
| 8 | Performance | `skills/code-review/references/performance-observability-testing.md` |
| 9 | Observability | `skills/code-review/references/performance-observability-testing.md` |
| 10 | Test Quality | `skills/code-review/references/performance-observability-testing.md` |
| 11 | Architectural Compliance | `skills/code-review/references/architecture-and-coding-standards.md` |
| 12 | Coding Standards | `skills/code-review/references/architecture-and-coding-standards.md` |
| 13 | API Design | `skills/code-review/references/api-design.md` |
| 14 | Code Formatting | `skills/code-review/references/code-formatting.md` |

**Always read first:** `severity-and-report.md`, `correctness-and-security.md`, `clean-code.md`, `performance-observability-testing.md`, `architecture-and-coding-standards.md`. Read others when the code under review touches their area. End every review with `technology-checklists.md`.

---

## 4. Correctness (always-on)

- Match every linked acceptance criterion. Missing AC = 🔴.
- No silent failures — `try/catch` must log or re-throw, never swallow.
- All edge cases handled — empty collections, zero values, null optionals, invalid IDs.
- No null reference risks — verify `.FirstOrDefault()`, optional chaining, nullable types.
- Async correctness — no `async void`, no `.Result`/`.GetAwaiter().GetResult()`.
- Transaction boundaries correct; no race conditions.

## 5. Security (always-on, OWASP Top 10)

- All user input validated/sanitized **at system boundaries**.
- Parameterized queries everywhere — no string concat in SQL.
- Authorization checked at the **resource** level, not just authentication at route level.
- Tenant isolation: a user cannot reach other tenants' data.
- No PII, tokens, passwords in logs/error responses/URLs.
- No hardcoded secrets, API keys, connection strings.
- Rate limiting on auth and sensitive mutation endpoints.
- CORS limited to expected origins.

## 6. Clean Code (always-on highlights)

- Names reveal intent. No `d`, `data`, `tmp`, `obj`, Hungarian notation, or mixed verb synonyms.
- Functions do **one thing**. <20 lines preferred, >40 lines is a finding. Max 3 parameters; no flag/output parameters; no side effects.
- Comments explain **why**, never **what**. No commented-out code. TODOs must link to a work item.
- DRY — no duplicated algorithms, business rules, or queries.
- Command-Query Separation — a function either does or answers, not both.
- No `null` returned where a collection or empty object would do.
- DTOs hold data only. Domain entities expose behaviour, hide data.

## 7. SOLID (always-on)

- **SRP:** One reason to change. Classes named `Manager`/`Helper`/`Utility` are smells.
- **OCP:** Extend by adding classes, not editing existing ones. No type-switching where polymorphism fits.
- **LSP:** No `NotImplementedException` in overrides. No `is`/`as` checks on base references in business logic.
- **ISP:** No fat interfaces (>5–7 methods is a smell). No empty/throwing implementations.
- **DIP:** All dependencies injected through constructors. No `new` for services. No static infrastructure (`DateTime.Now`, `File.ReadAllText`, `Environment.GetEnvironmentVariable`) in domain/application code.

## 8. Performance (always-on)

- No N+1 queries. Filter in the database, not in memory.
- Paginate large collections — never unbounded `.ToList()` on production data.
- No sync-blocking in async paths (`.Result`, `.Wait()`, `Thread.Sleep`).
- `AsNoTracking()` on read-only EF queries.
- `StringBuilder` for repeated concatenation.
- New query patterns require corresponding indexes.

## 9. Observability (always-on)

- Log key state transitions with correlation ID and structured fields.
- **No PII / tokens / passwords in logs.**
- Errors logged with full context **before** re-throwing — never log AND re-throw upstream (double logging).
- Log levels correct: DEBUG (dev noise), INFO (business events), WARN (recoverable), ERROR (failures).
- Structured logging only — `_logger.LogInformation("Processing {OrderId}", id)`, not string concat.

## 10. Test Quality (always-on)

- New/changed code has tests. No test-less PRs.
- Cover happy path + all error paths + authorization (401/403).
- Tests are independent — no shared mutable state, no ordering dependency.
- Test names describe scenario + expectation: `User_Deactivate_WhenAlreadyInactive_Returns409`.
- Arrange-Act-Assert. No logic (`if`/`for`/`switch`) inside tests.
- Integration tests verify DB state after writes, not just HTTP responses.
- Mocks at service boundaries only — never mock internal implementation details.

---

## 11. Architectural Compliance

- Implementation matches `docs/architecture/blueprints/` if a blueprint exists.
- Layer separation: domain logic in domain; no EF/HttpClient/framework attributes leaking into domain.
- Cross-cutting concerns wired through Pipeline Behaviors — not ad-hoc.
- No circular dependencies; no new libraries without architect approval.
- No binary files (`.docx`, `.pdf`, images) or backup files (`.bak`, `.orig`, `.old`) in source control.
- Folder/file casing consistent with existing project style.
- No dev connection strings or IPs in non-dev config.

---

## 12. Coding Standards (hard rules — flag every violation)

### Naming
| Rule | Severity |
|------|----------|
| `PascalCase` for classes, interfaces, records, enums, methods, properties, constants | 🟡 |
| `camelCase` for locals/parameters | 🟡 |
| `_camelCase` for private fields | 🟢 |
| Interfaces prefixed `I` | 🟡 |
| Async methods suffixed `Async` | 🟡 |
| No abbreviations except `Id`, `Url`, `Http`, `Dto`, `Api` | 🟢 |
| Generic params prefixed `T` | 🟢 |

### Type Declarations
| Rule | Severity |
|------|----------|
| **No `var`** — always explicit types | 🟡 |
| **No fully qualified type names** — use `using` directives | 🟢 |
| **File-scoped namespaces** (`namespace MyApp.Domain;`) | 🟢 |
| Explicit access modifiers on every member | 🟡 |
| `readonly` on never-reassigned fields | 🟢 |
| `const` for compile-time constants; `static readonly` for runtime constants | 🟢 |

### Nullability
| Rule | Severity |
|------|----------|
| `<Nullable>enable</Nullable>` in every project | 🟡 |
| Null-forgiving `!` requires a comment justifying it | 🟡 |
| Reference-type parameters guarded at boundary (`ArgumentNullException.ThrowIfNull`) | 🟡 |
| Nullable returns signalled in signature (`User?`) | 🟡 |

### XML Docs
- `<summary>` on every `public`/`protected` class, method, property, constructor — 🟡
- `<param>`/`<returns>`/`<exception>` when not self-explanatory — 🟢

### Code Quality
| Rule | Severity |
|------|----------|
| No magic strings/numbers — use constants/enums | 🟡 |
| String interpolation only — no `string.Format` or `+` concat | 🟢 |
| No `static` services or static state in business classes | 🔴 |
| DI for every dependency — no `new` for services | 🔴 |
| No `Thread.Sleep` in async paths — use `Task.Delay` | 🔴 |
| No hardcoded connection strings, secrets, or credentials | 🔴 |
| `using` directives sorted: System first, then alphabetical | 🟢 |
| Dispose `IDisposable` resources via `using` | 🔴 |

---

## 13. API Design

### Resource Naming
- Routes are **nouns**, plural, lowercase, kebab-case: `/api/v1/order-items`.
- Nested routes for owned children, max 2 levels: `/api/orders/{id}/items`.
- Filtering/sorting/pagination via query string — never route segments.
- API version in route prefix (`/api/v{n}/`), defined from day one.

### HTTP Verbs & Status Codes
| Action | Verb | Success Code |
|--------|------|--------------|
| Create | POST | 201 + `Location` header |
| Replace | PUT | 200 / 204 |
| Partial update | PATCH | 200 / 204 |
| Delete | DELETE | 204 |
| Read | GET | 200 |

| Scenario | Code |
|----------|------|
| Validation error | 400 + Problem Details |
| Unauthenticated | 401 |
| Forbidden | 403 |
| Not found | 404 |
| State conflict / duplicate | 409 |
| Business rule violated | 422 |
| Rate limited | 429 + `Retry-After` |

### Errors — RFC 7807 Problem Details
Every error returns `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, optional `errors`. Use `TypedResults.Problem()` or `ProblemDetails` — never hand-roll. Never expose stack traces (🔴).

### Responses
- Never return domain entities directly — always map to a DTO (🔴).
- Collection responses include pagination metadata.
- `Location` header on 201; `ETag`/`If-Match` on resources subject to concurrent modification.
- Omit null fields (`JsonIgnoreCondition.WhenWritingNull`) — don't return `"middleName": null`.

### Controllers
- `[ApiController]` on every controller; class-level `[Route("api/v{version}/[controller]")]`.
- Controllers: bind → call handler → map output → return. **Zero business logic.** (🔴 if violated.)
- Async actions when handler is async; accept and forward `CancellationToken`.
- Return `IActionResult`/`ActionResult<T>`/`TypedResults` — not raw types.
- `[ProducesResponseType]` documents every response code.

### Input Validation
- All input validated **at the API boundary** before the application layer (🔴 if business logic runs before validation).
- `[FromBody]`/`[FromRoute]`/`[FromQuery]` explicit when ambiguous.
- No business validation in controllers — only format/structure.

---

## 14. Code Formatting

- Indentation: 4 spaces, no tabs.
- Line length max 120; soft cap 100.
- One statement per line. Braces on their own line (Allman). Always brace `if`/`for`/`while`, even single-statement.
- Vertical openness — blank lines between concepts; no blank lines between a declaration and its immediate use.
- Variables declared near first use, not at the top of long methods.
- Caller above callee (stepdown rule).

---

## 15. DDD — Core (always-on for backend work)

1. Domain layer has **ZERO** dependencies on Infrastructure, Application, or any framework.
2. **No anemic domain models** — entities contain behaviour, not just getters/setters.
3. **Ubiquitous Language** — names match business domain language everywhere.
4. **Application Services are thin orchestrators** — business rules live in the domain.

Full reference: `references/ddd-standards.md`.

---

## 16. Definition of Done

- Compiles without warnings; explicit types; no TODO without ticket; no commented-out code.
- Unit tests for new business logic; passing locally; ≥80% coverage on business logic; edge cases covered.
- Code review approved; feedback addressed.
- XML docs on new public APIs; endpoint docs updated; README updated if setup changed.
- Migrations created and tested; no breaking API changes (or properly versioned); works in QA environment.

---

## 17. On-Demand References (load when relevant)

| Reference | When to read | Path |
|-----------|--------------|------|
| Severity model & report template | First, every review | `skills/code-review/references/severity-and-report.md` |
| Inline comment format | When commenting on PRs | `skills/code-review/references/comment-format.md` |
| Design patterns catalogue | When architecture-level code is reviewed | `skills/code-review/references/design-patterns.md` |
| OOP deep dive | Reviewing entity hierarchies, aggregates | `skills/code-review/references/oop-principles.md` |
| C# 2.0–13.0 features & anti-patterns | Reviewing C# code | `skills/code-review/references/csharp-modern-features.md` |
| Technology checklists (final pass) | End of every review | `skills/code-review/references/technology-checklists.md` |
| DDD standards | Backend implementation, architecture | `references/ddd-standards.md` |
| Quality bar | Consolidations, process checks | `references/quality-bar.md` |
| Estimation factors | Sprint planning, sizing | `references/estimation-factors.md` |
| Behavioral patterns | Phase transitions, DoD enforcement | `references/behavioral-patterns.md` |
| Phase summary protocol | Pipeline handoffs | `references/phase-summary-protocol.md` |
| Compliance frameworks | GDPR, SOC2, HIPAA, PCI-DSS | `skills/security-review/references/compliance-frameworks.md` |
| Token efficiency | Model routing, output caps, diff-only review, cache discipline, token telemetry | `references/token-efficiency.md` |
| Run log | Logging command runs; reading history for variance / retro / forecast analytics | `skills/run-log/SKILL.md` |
| Work-item naming | Always-on. Naming Epics, Features, User Stories, Tasks across BA, sprint planning, ADO sync | `references/work-item-naming.md` |
| Parallel execution | Any time a command/agent spawns >1 agent — fan-out patterns (P1–P6), concurrency map, worktree lanes, gate preservation | `references/parallel-execution.md` |
| Speculative execution | Pre-computing a phase while its gate is pending — the 6 invariants, the hash interlock, the predictor | `references/speculative-execution.md` |
| Review engines | Step 0 of `/review`, `/review-pr`, `/review-adversarial` — Claude / Codex / Both, preflight, reconciliation, fallback | `references/review-engines.md` |
| Cross-engine debate | `/cross-debate` — Claude (fable) vs Codex (top model), claim ledger, convergence rule, Codex model resolution, refuse-by-default fallback | `references/cross-engine-debate.md` |
| Self-healing loops | Always-on. Any objective checker (build, tests, lint, contract) — bounded `verify → fix → re-verify`, checker registry, objective/subjective split, `kind=heal` telemetry | `references/self-healing.md` |
| Live narration | Always-on. Badge grammar for surfacing every skill/agent/gate/heal/spec event as a one-line chat badge | `references/live-narration.md` |

---

## 18. Self-Healing Loops (always-on)

Every **objective, mechanical** checker any command/agent/skill runs — build, task tests, formatter/linter, contract tests, browser-walkthrough mechanics — is wrapped in a bounded `verify → fix → re-verify` loop instead of failing straight to the human. Full doctrine: `references/self-healing.md`. Engine: `workflows/heal-loop.js`.

1. **Objective checkers only.** A checker heals only when the judge is deterministic (compiler, test runner, linter, frozen contract). **Subjective verdicts never self-heal** — "matches the story/AC", review verdicts, and QA gates go to an independent evaluator and a human gate, never back to the author for re-grading.
2. **Bounded.** Attempt budgets are pinned per checker in the `CHECKERS` registry (default 2 same-tier attempts, at most one model-tier bump up to the ceiling). Never raise a budget inline; changes ride `/learn`'s Step-4 HARD GATE.
3. **Zero gates in the loop body.** A heal loop never writes `docs/.gates/*`, never emits an approval, never advances a phase. On exhaustion it STOPS and escalates to an **existing** terminal (reopen the plan gate, present the failing report) — escalation, never approval.
4. **The fix moves the code, never the judge.** Scoped to the failing files; weakening/deleting a failing assertion, loosening a lint rule, or editing the frozen OpenAPI fragment to get green is forbidden.
5. **Every heal is logged** as a `kind=heal` run-log event; `/learn` Step 2d tunes attempt budgets and flags chronic offenders.

---

## 19. Live Narration (always-on)

Every meaningful framework event is announced as a **one-line badge in chat, at the moment it happens** — the user should never need transcript mode or `runs.jsonl` to know what just ran. Full doctrine + badge grammar: `references/live-narration.md`.

1. **Hooks announce the mechanical events** — skill invoked 🧩, agent spawned 🤖, agent finished ✅ (model · duration · tokens), workflow launched 🛠, slash-command model tier `[model: X]`. These fire from `hooks/announce.sh` / `log-subagent-stop.sh` / `prompt-model-badge.sh` and cannot be forgotten.
2. **Claude announces everything hooks can't see** — reference loaded (`▸ ref:`), phase transition (`▶ phase:`), gate reached/approved (`⛔`/`🟢 gate:`), heal attempts (`♻ heal: attempt n/N`), speculative pre-compute (`⚡ spec:`), learnings (`📚 learn:`), review-engine choice (`⚖ engine:`). One line per event, ≤80-char detail, emitted inline — never batched into an end-of-run summary.
3. **Narration is display, never control.** A badge never advances a gate, never counts as approval, never replaces the `runs.jsonl` telemetry record.
4. **Off switch:** user says `narration off` / `narration on` for the model-emitted badges; hook lines are governed by `settings.json`.

---

## 20. Work-Item Naming — Core Rules (always-on)

Every Epic, Feature, User Story, and Task created by any agent or command uses the hierarchical naming standard. **No flat IDs** like `EPIC-01`, `STORY-42`, `TASK-7`.

| Type | Format | Example |
|------|--------|---------|
| Epic | `E{e}: {Title}` | `E1: Foundation & Infrastructure` |
| Feature | `F-{e}.{f}: {Title}` | `F-1.1: Solution Scaffold` |
| User Story | `US-{e}.{f}.{s}: {Title}` | `US-1.1.1: Set Up Solution` |
| Task | `T-{e}.{f}.{s}.{nn} [{TYPE}] {Title}` | `T-1.1.1.01 [INF] Create solution file` |

- Epic/Feature/Story ordinals: **1-based, no padding.** Task ordinals: **2-digit zero-padded.**
- IDs are **stable forever** — never re-used, never renumbered, even after cancellation or splits.
- Task type tags are mandatory: `[INF]`, `[DOM]`, `[DAT]`, `[API]`, `[INT]`, `[UI]`, `[UX]`, `[TEST]`, `[DOC]`, `[SEC]`, `[PRF]`, `[OBS]`, `[REL]`.
- The same ID appears verbatim in ADO work items, markdown docs, sprint plans, branch names, commit messages, PR titles, and test names.

Full standard (splitting, re-parenting, type-tag catalogue): `references/work-item-naming.md`. A project's local `CLAUDE.md` may override only when its work-item tool requires a different primary ID format; the hierarchical IDs are then preserved as secondary identifiers.
