# Compatibility decisions

## Classify the change before selecting a mechanism

| Change | Assess |
|---|---|
| Internal refactor with unchanged semantics | Whether callers, exception behavior, synchronization, or allocations actually remain equivalent |
| Performance optimization | Correctness and measured behavior under the affected runtime, build mode, cache lifetime, and workload |
| Narrow bug fix | Which existing inputs relied on the old behavior, and whether an opt-out is useful |
| New explicit opt-in | Whether diagnostics and changed behavior stay inside the opted-in path |
| Changed default, parsing, execution order, or output | Existing project/SDK assumptions and a practical mitigation or migration path |
| Public API, CLI, or property removal | Source/binary/tooling contracts; a runtime flag cannot restore a missing declaration |

A ChangeWave groups risky, default-on behavior behind a temporary opt-out. It can make a justified transition manageable, but it does not prevent a break for users who have not opted out. The implementation enables all waves when the environment variable is unset: see `ApplyChangeWave` and `AreFeaturesEnabled` in [ChangeWaves.cs](../../../../src/Framework/ChangeWaves.cs).

New opt-in functionality can appropriately reject invalid input with errors or warnings. Check that the opt-in is real: a new API parameter or property is not isolation if an existing build starts reaching the new diagnostic by default.

## Warning promotion is not one property

| Setting | Relevant contract |
|---|---|
| CLI `-warnAsError` without codes | Promotes warnings through the engine across the build, subject to configured exceptions |
| CLI `-warnAsError:code1;code2` | Promotes the specified codes, not every new warning |
| `MSBuildTreatWarningsAsErrors` | Project-level engine warning promotion |
| `MSBuildWarningsAsErrors` / `MSBuildWarningsNotAsErrors` | Project-level code lists consumed by the engine |
| `TreatWarningsAsErrors` | Passed to compilers and some tasks; not the general engine switch for all MSB warnings |
| `WarningsAsErrors` / `WarningsNotAsErrors` | Also used by compiler/task contracts; Common targets supply these as defaults for the corresponding MSBuild code lists |

Sources:
- [LoggingService.ShouldTreatWarningAsError](../../../../src/Build/BackEnd/Components/Logging/LoggingService.cs) contains global/project promotion and exception handling.
- [MSBuildConstants](../../../../src/Framework/MSBuildConstants.cs) identifies the engine property names.
- [Common targets](../../../../src/Tasks/Microsoft.Common.CurrentVersion.targets) map the code-list defaults; [C# targets](../../../../src/Tasks/Microsoft.CSharp.CurrentVersion.targets) pass compiler settings.
- [WarningsAsMessagesAndErrors_Tests](../../../../src/Build.UnitTests/WarningsAsMessagesAndErrors_Tests.cs) cover selected codes, additive properties, project contexts, and task-host behavior.

Use the setting that exercises the changed logging path. Merely setting `TreatWarningsAsErrors` in an arbitrary project is not evidence that engine warning promotion was tested.

An informational `BuildMessageEventArgs` is not promoted by the engine's warning promotion. However, moving an existing warning to a message changes observability and tooling contracts. Choose message importance for its audience; it is separate from diagnostic severity. For event-content limits, see [binary-log event content](../../maintaining-binary-log-compatibility/references/event-content.md).

## Concrete blast-radius questions

Select the questions relevant to the change rather than executing every row.

| Boundary | Useful evidence |
|---|---|
| Build result or generated output | Same input, baseline and candidate result, output contents and paths |
| Incremental behavior | Relevant clean, source-edit, and no-op cases; do not assume every target should be skipped on a second build |
| Target/project-reference ordering | Dependency/extension contracts and the actual CLI or IDE invocation |
| Evaluation | Property precedence, item ordering, imports, escaping, and affected project APIs |
| Logging | Event kind/code, importance, location metadata, text consumers, and log-size/cost implications |
| Exceptions | Type, timing, error code, cleanup, and whether a caller continues or exits |
| Platform or TFM | The affected configurations from current props, including reference-only and source-build exclusions where relevant |
| Host/process lifetime | Standalone MSBuild, in-process hosts, reused nodes, task hosts, or multithreaded execution as implicated by the change |

For example, replacing a COM exception with `false` is not automatically equivalent just because some caller catches COM exceptions. The exception may have skipped later work, and another caller may rely on it propagating.

For execution evidence, use the [test workflow](../../running-unit-tests/SKILL.md) or [bootstrap reproduction](../../use-bootstrap-msbuild/SKILL.md) only when the question requires it. Identify the binaries and scenario actually exercised.

## Deprecation and removal

Do not invent a universal number of releases or major .NET versions for retaining behavior. Agree on the particular contract, consumers, migration, and release policy.

Deprecation warnings and compiler-obsolete annotations can themselves break warning-as-error consumers. A ChangeWave can gate runtime behavior; it cannot make removing a public API binary-compatible.

Wave rotation is a separate lifecycle described in [ChangeWaves.md](../../../../documentation/wiki/ChangeWaves.md). Retiring a wave does not authorize removing unrelated APIs, CLI switches, or properties.

## Decision record

Keep the handoff specific: old behavior, new behavior, affected users, rationale, mitigation or explicit acceptance of risk, evidence obtained, and remaining gaps. Do not replace these with a blanket "non-breaking" label.
