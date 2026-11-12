---
applyTo: "src/TaskAnalyzer/**/*.cs,src/TaskAnalyzer.Tests/**/*.cs"
---

# Multithreaded-Task Analyzer Instructions

Layered on top of [roslyn-analyzers.instructions.md](./roslyn-analyzers.instructions.md). Those rules cover the analyzer *engineering*; these cover the **MSBuild multithreaded-task semantics** the `MSBuildTaskNNNN` rules encode. Every rule here is a *domain* rule — get it wrong and the analyzer is confidently incorrect. Ground truth: the `multithreaded-task-migration` skill (`plugins/mt-migration/`) and `documentation/specs/multithreading/`.

## The two orthogonal signals (never conflate them)

* `[MSBuildMultiThreadableTask]` = **routing**. It is the MT-safety routing signal that lets a task run **in-process** rather than in an out-of-proc TaskHost (absent other host requirements). `TaskRouter` reads it by **full type name**, `inherit: false`, off the concrete instantiated type (so a task may polyfill its own attribute). The declaration analyzer instead matches the resolved **framework attribute symbol** (`SymbolEqualityComparer`), also direct/non-inherited — mind this name-vs-identity difference when changing attribute detection.
* `IMultiThreadableTask` = **injection**. The engine assigns the `TaskEnvironment` property **after construction**, only to instances of the interface. A public single-`TaskEnvironment` constructor is a separate mechanism: injected **during construction** (selected by signature; the constructor must assign the property itself). The interface is deliberately **not** a routing signal because `ToolTask` implements it — so interface-based rules (0013) key on the interface in the type's **own** base list, not inherited.
* **Attribute-only** (attribute, no interface, no `TaskEnvironment` property) is a *supported declaration state* — no 0011–0014 diagnostic — and is a correct migration when the task has no file/env/process/static use (still checked by 0001–0005). No rule, message, or fix may imply the interface is always required.

## What each rule class means (keep rationale intact)

* **0001 (Error) — never-safe, whole-process APIs**: `Console.*` (banned at the *type* level), `Environment.Exit`/`FailFast`, `Process.Kill`, `ThreadPool.Set*`, `CultureInfo.DefaultThreadCurrent*`, `Directory.SetCurrentDirectory`. Unsafe regardless of MT → **must fire on every `ITask`; never scope-gate these.**
* **0002 (Warning) — per-task process state with a `TaskEnvironment` replacement**: cwd, env vars, `Path.GetFullPath`/temp, `Process.Start`/`new ProcessStartInfo`.
* **0003 (Warning) — relative path against the shared cwd**: file-system APIs on the monitored types. The monitored-type list is **knowingly incomplete** (see blind spots).
* **0004 (Warning) — assembly loading** (version conflicts in a shared host): **review-required**, not never-safe. Unscoped like 0001 — fires on every `ITask`; don't scope-gate.
* **0005 (Warning, `CompilationEnd`) — transitive** reach through helpers; reported at the unsafe call site with the task entry as an `AdditionalLocation`.
* **0006–0008 (Info), 0009–0010 (Warning), 0011–0014**: typed-parameter modernization, `ITaskItem<T>` binding, and the routing/injection truth table.

## Messages must be honest

* State only what the engine **guarantees**. Don't assert a runtime value it doesn't set (say "MSBuild never assigns it / retains its default", not "stays `TaskEnvironment.Fallback`").
* Never imply that applying the attribute alone makes a task safe — it is the *last* step, a claim of concurrency-safety. **Do not offer a code fix that auto-applies `[MSBuildMultiThreadableTask]`.**

## Path semantics

* `TaskEnvironment.GetAbsolutePath` roots against `ProjectDirectory` and **does not canonicalize** (unlike `Path.GetFullPath`). Rooting must be **unconditional** — an `IsPathRooted` gate is a bug (misses Windows drive-/root-relative forms). Recognized safe forms: `GetAbsolutePath(...)`, `AbsolutePath`/`Nullable<AbsolutePath>` implicit conversion, `FileInfo/DirectoryInfo.FullName`, `ITaskItem.GetMetadata("FullPath")`, first arg of `Path.Combine`, an already-`AbsolutePath`-typed argument.
* Use `AbsolutePath.OriginalValue` for messages/`[Output]`, `Value` for file I/O.

## TaskEnvironment injection timing

* Assigned **after** construction → property initializers and constructor bodies can't use it. That is why 0008 (root a relative default in `Execute()`) and 0011 (prefer constructor injection) exist. Any fix referencing `TaskEnvironment` must be **withheld** where it can't bind: static context (CS0120), initializers (CS0236/CS0027), or a task with no `TaskEnvironment` member (`Microsoft.Build.Utilities.Task` — CS0103).

## Scope & configuration

* Genuinely MT-specific rules (0002/0003/0005) may be gated by `msbuild_task_analyzer.scope` (`all` | `multithreadable_only`). Keep 0001/0004 unscoped; do not gate them. Expose the option through a reachable `CompilerVisibleProperty` + `build/*.props`, not a raw dotted `build_property` name. Prefer per-rule severity config over new scope values.

## Known blind spots (do not pretend to cover them)

The analyzer is a partial static approximation of a dataflow+lifetime property. Out of reach **by design**: unannotated base classes (`Inherited=false`; base-class coverage may be extended over time), analyzer-invisible path consumers (`AssemblyName.GetAssemblyName`, `XDocument.Load`, `ZipFile.*`, …), DI/delegate boundaries, `RegisterTaskObject` races, process-seeded `static` fields, nested `new MyTask().Execute()`, and the behavioral-parity "8 sins". A clean analyzer run is not proof of a safe migration.

## Related Documentation

* [Rule catalog & rationale](../../src/TaskAnalyzer/README.md)
* [Reviewing MT analyzer contributions (skill)](../skills/reviewing-multithreaded-task-analyzers/SKILL.md)
* [MT task migration playbook (skill)](../../plugins/mt-migration/skills/multithreaded-task-migration/SKILL.md)
* [Multithreading specs](../../documentation/specs/multithreading/)
