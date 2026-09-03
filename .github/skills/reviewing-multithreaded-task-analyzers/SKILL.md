---
name: reviewing-multithreaded-task-analyzers
description: "Use when reviewing a PR that adds or changes an MSBuildTaskNNNN diagnostic in the MSBuild thread-safe task analyzer (src/TaskAnalyzer/, Microsoft.Build.TaskAuthoring.Analyzer) — the rules that steer task authors toward IMultiThreadableTask / TaskEnvironment / [MSBuildMultiThreadableTask]. Layers on top of reviewing-roslyn-analyzers to judge whether the encoded rule matches MSBuild multithreaded-task semantics: attribute-vs-interface routing/injection, TaskEnvironment injection timing, AbsolutePath path-rooting, banned-API rationale, scope-gating of unscoped vs MT-specific rules, config reachability, and the hazards the analyzer cannot model by design."
argument-hint: "Paste the analyzer PR number/URL or the MSBuildTaskNNNN rule diff to review."
---

# Reviewing Multithreaded-Task Analyzer Contributions

This is the **specialized layer** on top of **reviewing-roslyn-analyzers**. That skill judges whether the analyzer is well-built (registration, descriptors, release tracking, fixes, tests). This skill judges the harder question: **does the encoded rule actually match MSBuild multithreaded-task semantics?**

The single most important framing: **MT-safety is a dataflow + lifetime property** — "does any task input reach a process-global sink, through any path, at any point in the object's life?" — and it is only *partially* expressible as banned-symbol / AST rules. The analyzer is a deliberately incomplete static approximation of the ground truth. The ground truth lives in the **multithreaded-task-migration** skill; treat that skill as the specification and the analyzer as the (partial) enforcement.

## When to use

- A PR adds, removes, widens, narrows, or re-scopes an `MSBuildTaskNNNN` rule, its banned-API list, its safe-pattern set, its message text, or its code fix.
- A PR changes analysis **scope** (`all` vs `multithreadable_only`) or the config surface that controls it.
- Always run **reviewing-roslyn-analyzers** first (Waves 1–5); this skill adds **Wave 6**.

Not for: reviewing a *task* being migrated to MT — that is the `mt-migration-reviewer` agent's job, driven by the `multithreaded-task-migration` skill.

## Load the domain first

Before judging a rule, load **multithreaded-task-migration** (the 8 compatibility sins, the leaf-API hazard table, the decoy-CWD / cross-instance test patterns). A rule is only "correct" if it rewards the shape that skill calls correct and flags the shape it calls a defect.

## The MT model on one screen (verify a rule against this)

**Two orthogonal signals** (`TaskRouter.NeedsTaskHostInMultiThreadedMode`, spec `documentation/specs/multithreading/thread-safe-tasks.md`):
- **Attribute `[MSBuildMultiThreadableTask]` = routing.** It, and *only* it, lets a task run **in-process** rather than in an out-of-proc TaskHost (absent other host requirements). `TaskRouter` reads it with `inherit: false` off the concrete instantiated type, matching by **full type name** (so a task may polyfill its own attribute). The declaration analyzer instead matches the resolved **framework attribute symbol** (`SymbolEqualityComparer`), also direct/non-inherited — see the name-vs-identity discrepancy check in Wave 6.
- **Interface `IMultiThreadableTask` = injection.** The engine assigns the `TaskEnvironment` property **after construction**, only to instances of this interface. Separately, a public single-`TaskEnvironment` constructor is injected **during construction** — the engine selects it by signature and the constructor must assign the property itself. The two mechanisms are independent.
- The interface is deliberately **not** a routing signal because `ToolTask` implements it — routing on it would silently opt in every `ToolTask`.

**Routing/injection truth table** (what 0011–0014 encode):

| Attribute | Interface / TE ctor | Effect | Correct diagnostic |
|---|---|---|---|
| ✅ | ✅ concrete, no TE ctor | in-proc, env injected post-construction | 0011 Info — prefer ctor injection |
| ✅ | ❌ but has (incl. inherited) a settable `TaskEnvironment` property, no TE ctor | in-proc, **engine never assigns it** → resolves against shared CWD | 0012 Warning |
| ❌ | ✅ in own base list | correct paths but **still out-of-proc** (no perf win) | 0013 Info, off by default |
| ✅ on a non-`ITask` class, or an abstract `ITask` class | — | attribute reaches nothing the engine routes | 0014 Warning |
| ✅ | ❌, no TE property at all | no **declaration** diagnostic — "migrated" only if 0001–0005 + a call-chain audit are clean | none (0011–0014) |

**TaskEnvironment** (`src/Framework/TaskEnvironment.cs`): `ProjectDirectory`, `GetAbsolutePath(string)→AbsolutePath`, `Get/Set/GetEnvironmentVariables`, `GetProcessStartInfo()`, static `Fallback` (backed by `MultiProcessTaskEnvironmentDriver` → delegates to real process state; the correct single-tenant default). Assigned **after construction**, so initializers/ctor bodies can't use it → the constructor-injection pattern (0011) and relative-default-in-`Execute()` (0008) exist for exactly that.

**AbsolutePath** (`src/Framework/PathHelpers/AbsolutePath.cs`): `Value` (absolute) vs `OriginalValue` (input as-provided, for messages/`[Output]`); implicit→string; **does not canonicalize** (`..` unresolved unless `GetCanonicalForm()`); throws on null/empty.

**Banned-API rationale = shared process state:** process-terminators / process-wide setters (Console, `Environment.Exit`, `Process.Kill`, `Directory.SetCurrentDirectory`, `DefaultThreadCurrentCulture`, `ThreadPool.Set*`) are never safe → **0001 Error**. Assembly loading (version conflicts in a shared host) is **review-required**, not never-safe → **0004 Warning**. Both 0001 and 0004 fire on **every** `ITask` regardless of scope. Per-task-varying process state (cwd, env, `Path.GetFullPath`/temp, `Process.Start`) → **0002 Warning**; relative paths against the shared cwd → **0003 Warning**; transitive reach through helpers → **0005 Warning** (`CompilationEnd`) — these three are the MT-scoped rules.

---

## Wave 6 — MT-domain correctness

Apply after Waves 1–5. Add a **W6 — MT semantics** coverage row with `CLEAN / ISSUES / N/A / INCOMPLETE`. `CLEAN` requires all applicable checks; `INCOMPLETE` never becomes a finding. Report every rule defect proved by a concrete task shape or engine behavior; no cap or quota. The same evidence-and-noise gate applies.

**Routing / injection semantics**
- [ ] Attribute detection is **direct** (`inherit: false`) off the concrete type. Note the deliberate name-vs-identity split: `TaskRouter` matches by **full type name** (a polyfilled attribute still routes in-proc), while the declaration analyzer matches the resolved **framework attribute symbol** (a polyfilled attribute routes but is *not* seen by 0012–0014). If a rule change touches attribute detection, confirm which of the two behaviors it should follow. Injection rules key on the interface in the type's **own** base list (not inherited via `ToolTask`), or on a single-`TaskEnvironment` constructor.
- [ ] Does the rule respect that **attribute-only** (attribute, no interface, no `TaskEnvironment` property) is a *supported declaration state*? It produces no 0011–0014 diagnostic, and is a correct migration when the task has no file/env/process/static use (still checked by 0001–0005 and a call-chain audit). A rule/message/fix implying the interface is always required is wrong — a simple task may need only the attribute and still be fully and properly migrated.

**Message honesty**
- [ ] The message/description states only what the engine **guarantees**. Don't assert a runtime value the engine doesn't set (e.g. "stays `TaskEnvironment.Fallback`" — say "MSBuild never assigns it / retains its default").
- [ ] The message doesn't imply that applying `[MSBuildMultiThreadableTask]` alone makes a task safe. Adding the attribute is a *claim* of concurrency-safety and is the **last** step, not the fix — it is not the only thing a task needs to be MT-safe.
- [ ] There is **no code fix that auto-applies `[MSBuildMultiThreadableTask]`.** Friction is intentional — auto-applying a safety claim without understanding the task is harmful.

**Unscoped vs MT-specific (scope-gating)**
- [ ] The **unscoped** rules — 0001 (never-safe: `Environment.Exit`/`Console.ReadLine`/…) and 0004 (review-required: assembly loading) — keep firing on **all** `ITask`; they must **not** become scope-gated. Only the genuinely MT-specific rules (0002/0003/0005: path/env/cwd) are gated to MT tasks. Name exactly which tasks a scope change newly breaks or newly ignores — a consumer updating `Microsoft.Build.Framework` must not gain un-opt-out-able build errors.

**Path / AbsolutePath semantics**
- [ ] Safe-pattern recognition matches the domain: `GetAbsolutePath` ≠ `Path.GetFullPath` (no canonicalization), and rooting must be **unconditional** — an `IsPathRooted` gate is a *bug*, not a safe pattern (misses Windows drive-/root-relative forms). Recognized safe forms: `GetAbsolutePath(...)`, `AbsolutePath` (and `Nullable<AbsolutePath>`) implicit conversion, `FileInfo/DirectoryInfo.FullName`, `ITaskItem.GetMetadata("FullPath")`, an already-`AbsolutePath`-typed argument. (Sins 5 & 7.)
- [ ] `Path.Combine` is treated as safe only via its **first** argument (later positions restart from the last rooted segment).

**TaskEnvironment binding & code-fix withholding**
- [ ] Any fix that references `TaskEnvironment` is **withheld** where it can't bind: static context (CS0120), initializers (CS0236/CS0027), or a type whose only `ITask` implementation is `Microsoft.Build.Utilities.Task` (no `TaskEnvironment` member — CS0103).
- [ ] Injection-timing rules (0008 relative default, 0011 ctor injection) correctly account for `TaskEnvironment` being unavailable before construction completes.

**`ITaskItem<T>` binding**
- [ ] 0009 (unsupported `T`) and 0010 (culture-sensitive `Convert.ChangeType` types) match the binder's actual supported set (`string, bool, AbsolutePath, FileInfo, DirectoryInfo` directly parsed; numeric/char/`DateTime` via invariant `Convert.ChangeType`). Open generics (`TypeKind.TypeParameter`) must be skipped.

**Config reachability & cost**
- [ ] The scope/config option is actually **reachable**. The analyzer reads `msbuild_task_analyzer.scope` from analyzer global options; a raw dotted `build_property.msbuild_task_analyzer.scope` has no reachable MSBuild property, so exposing the option needs a `CompilerVisibleProperty` + packed `build/*.props` (verify the actual wiring). Prefer existing per-rule **severity** config (`.editorconfig`/`.globalconfig`) over inventing new scope values. Note `MSBuildTask0005` runs at `CompilationEnd` with no syntax tree, so per-tree `.editorconfig` scope would disagree with it.
- [ ] The analyzer doesn't pay for analysis it discards (e.g. building the whole transitive call graph then dropping it under a default scope).

**Empirical verification**
- [ ] Any claim about engine behavior (injection timing, what routes where, whether an env var takes effect) is **verified**, not assumed. When the semantics are subtle, ask for a test or a spec/source citation rather than trusting intuition.

## Known blind spots — what the analyzer cannot model (do not accept "analyzer is clean" as proof; do not demand it model these either)

The analyzer is a partial static approximation of an MT-safety property that is fundamentally dataflow + lifetime. As a calibration point, a large migration with this analyzer enabled and a clean 0/0 build can still contain real defects the analyzer cannot see. When a rule change claims to "cover" MT-safety, remember these residual hazards live outside static reach **by design**:
- **Unannotated base classes** run multithreaded but aren't analyzed under `multithreadable_only` (`Inherited=false`); base-class coverage may be extended over time, so verify the current analyzer scope.
- **Analyzer-invisible path consumers** not on the 0003 monitored list: `AssemblyName.GetAssemblyName`, `XDocument/XmlDocument.Load(string)`, `XmlReader/XmlWriter.Create(string)`, `ZipFile.*`, `X509CertificateLoader`, `Image.FromFile`, `Assembly.LoadFrom` (string overloads).
- **Dataflow/lifetime hazards:** task inputs crossing a DI/interface/delegate boundary; `RegisterTaskObject`/`GetRegisteredTaskObject` races; process-state-seeded `static` fields; nested `new MyTask().Execute()`; and the behavioral-parity **8 sins** (`[Output]`/message inflation, `?? ""` control-flow changes, canonicalization/exception-type changes).

So: a widened allow-list can never make the analyzer "complete" — value it as one layer, and route dataflow/lifetime review to the `mt-migration-reviewer` agent.

## MT review invariants (reapply on every analyzer PR)

These recur across analyzer reviews:

1. No auto-fix that applies `[MSBuildMultiThreadableTask]` — it's the last step, not the fix.
2. Messages must be MT-accurate and honest; attribute-only can be a complete migration.
3. Keep the unscoped rules (0001 never-safe, 0004 review-required) firing on all tasks; only gate the genuinely MT-specific ones (0002/0003/0005).
4. Minimize config surface; prefer existing severity knobs; make any option reachable.
5. Verify engine behavior empirically before asserting it in a message, doc, or rule.

## Sign-off (MT layer)

- [ ] Every new/changed rule verified against the routing/injection truth table and the banned-API rationale.
- [ ] Messages state only engine-guaranteed behavior; no rule/message/fix implies the attribute alone is sufficient.
- [ ] Unscoped vs MT-specific scoping is correct; no un-opt-out-able new break for plain `ITask` consumers.
- [ ] Path safe-patterns match domain semantics (no `IsPathRooted` gate; `GetAbsolutePath` ≠ canonicalize).
- [ ] Config option is reachable; no bespoke knob where severity config suffices.
- [ ] Residual dataflow/lifetime hazards acknowledged, not assumed covered.

## Cross-references

- Generic analyzer engineering: **reviewing-roslyn-analyzers** skill.
- MT domain ground truth (sins, hazards, test patterns): **multithreaded-task-migration** skill (`plugins/mt-migration/`).
- Reviewing a *task* migration (call-chain audit): **`mt-migration-reviewer`** agent.
- Automated multi-wave analyzer review: **`@analyzer-reviewer`** agent (runs Waves 1–6).
- Rule catalog: `src/TaskAnalyzer/README.md`. Spec: `documentation/specs/multithreading/`.
