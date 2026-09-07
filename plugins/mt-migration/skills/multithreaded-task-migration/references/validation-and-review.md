# Validation and review evidence

Use this reference to choose meaningful migration evidence and report its limits. It does not authorize builds, new test tooling, publication, or broad execution outside the requested scope.

## Preflight the existing harness

Read the checkout's SDK/runner configuration, task/test project imports and TFMs, existing fixtures, and relevant host-specific instructions. Use the repository's local-test and product-reproduction guidance when available. Do not require a fixed Framework/.NET pair when the task or host does not support both.

Current MTP documentation uses `--project`/`--solution` selection and supports `--` for runner arguments. Extensions and filters depend on the actual runner/version. Do not copy a VSTest selector into an unrelated runner or treat the separator itself as invalid.

In the inspected MSBuild test helpers, `TaskEnvironmentHelper.CreateForTest()` returns **`TaskEnvironment.Fallback`**. It provides a usable environment, not MT isolation. Tests relying on process CWD can still pass after an environment-plumbing regression.

`TaskEnvironment.CreateWithProjectDirectoryAndEnvironment` provides an isolated test environment in the current Framework implementation. Verify availability, setup, and cleanup in the target host; the implementation also affects a thread-local path-resolution context.

## Match evidence to the claim

| Claim | Relevant evidence |
| --- | --- |
| Path/environment plumbing | A task sees the intended project inputs despite a different ambient directory/environment |
| Compatibility | Baseline/candidate agree on required outputs, diagnostics, failure behavior, and side effects |
| Constructor/property injection | The selected factory supplies the expected environment at the point it is used |
| Annotation/host eligibility | The real engine selects the intended route for the supported host/mode |
| Cache or exactly-once behavior | An interleaving exposes or rules out the relevant duplicate/stale result, with scope and ownership established |
| Child-process behavior | The selected tool actually observes the required directory/environment/arguments |

Source reasoning, constructed-object assertions, and executable evidence have different reach. A clean analyzer result contributes static evidence but cannot replace an unrelated runtime assertion.

## Pattern A: decoy directory

Choose a task that genuinely reads or writes a project-relative path.

1. Create a project directory with a distinguishable input and a separate decoy directory.
2. Configure the task's environment to the project directory while the process CWD is the decoy.
3. Run the relevant task operation through its existing harness.
4. Assert the meaningful output: correct content/classification, destination, diagnostic, or remaining batch work.
5. Assert that the decoy was not read/written when that is part of the contract.

Use an existing transient-directory/working-directory fixture. In MSBuild, `TestEnvironment.SetCurrentDirectory` records a `TransientWorkingDirectory` that restores process CWD; restoration alone is not concurrent-test isolation.

An assertion sketch after arranging distinct project/decoy inputs:

```csharp
task.Execute().ShouldBeTrue();
File.ReadAllText(expectedOutputPath).ShouldBe("project-specific result");
File.Exists(decoyOutputPath).ShouldBeFalse();
```

Adapt properties and paths to the real task; this is not a generic complete test. If the task treats missing/wrong files as a successful semantic answer, `Execute().ShouldBeTrue()` alone proves almost nothing about the migration.

## Pattern B: cross-instance independence

Create two task instances with different project-directory/environment inputs and the same relative filename. Give each input distinguishable content or expected results.

Assert that each instance observes its own intended values. This can isolate project-context handling without mutating process CWD, but does not alone prove concurrent cache correctness or scheduling.

For a cache/registration race, add only the synchronization or concurrent scenario needed to exercise the problematic interleaving. Check the resulting number of side effects, retained value, failure scope, and disposal, rather than relying on elapsed time or a nondeterministic stress loop.

## Preserve isolation and useful compatibility tests

Process CWD and process environment are shared state. Check the actual collection/assembly configuration and other tests that can run concurrently. Do not assume methods in one class run concurrently by default, and do not disable repository-wide test isolation/parallelism to make a test appear faster.

Where required, place process-state-mutating tests in the repository's supported nonparallel collection or use an existing process-isolated harness. Restore process and relevant thread-local state through the owning fixture.

Reuse `TestEnvironment`/transient helpers where available instead of inventing manual cleanup that leaks on failure. In another repository, discover its equivalent rather than requiring an MSBuild-only helper.

Keep expected results independent of the production algorithm. Computing a temp-directory-based expected path with `Path.Combine` is fine; recomputing the migration's resolution logic as the assertion oracle is not.

A test passing both baseline and candidate may be a valuable compatibility test. It is simply not, by itself, a regression-sensitive assertion of the new plumbing. Do not label every such test defective.

## Attribute-only and substantive changes

For substantive plumbing changes, identify an assertion that fails when the relevant change is removed or replaced by fallback under the chosen scenario. Establish that comparison when feasible and authorized, or report the missing baseline evidence.

For attribute-only changes with no relevant path/environment work, a decoy-directory test may add no signal. Do not manufacture one. The annotation still affects hosting: determine whether existing metadata/routing coverage is sufficient or whether the requested claim needs an engine scenario.

Non-migration is a valid disposition when the shared-process contract cannot be supported. It does not automatically prove another host's isolation or the rest of the diff correct.

## Engine and host evidence

Use the actual task assembly and selected MSBuild binary/host. Record source identity, effective mode (including `-mt` where relevant), factory/registration path, and the assertion distinguishing the expected route.

Normal reflection-based construction, registered factories, explicit task hosts, separate-AppDomain paths, IDE host objects, and direct `new` can have different setup. Cover the boundaries affected by the change rather than claiming one task unit test proves all of them.

Use existing host/constructor integration helpers where available. Do not confuse the modern sidecar task host with a differently scoped legacy task-host executable.

Start with the smallest existing test/reproduction that establishes the requested claim. Do not install tooling, change SDK/feed configuration, run an unconditional full build, or start a generic test-generation pipeline. If execution is disallowed or prerequisites are unavailable, report source-derived conclusions and the remaining executable proof without attempting an unauthorized workaround.

## Review result

For each actionable finding, provide:

```text
Severity and concise defect
Location: responsible changed file:line; name off-diff helpers separately
Chain: construction/Execute/override -> helper/base/abstraction -> leaf
Conditions: actual input, environment, host, and relevant interleaving
Impact: changed output, diagnostic, exception, side effect, or host behavior
Evidence: source-derived / observed / unresolved, with the supporting artifact
Repair: the narrow contract-preserving change
```

Blocking severity needs a concrete reachable failure/impact argument, not merely a hazard-table match. Missing evidence is not proof of a production defect. Separate uncertainty from confirmed findings and avoid duplicating a general review.

Return the reviewed scope and material unresolved boundaries. Do not equate silence, an analyzer pass, or a no-findings result with all scenarios covered. Stop when the requested boundaries have an evidence-backed disposition; continue only for a specific gap.

Prepare a local report by default. Posting reviews/comments, approvals, resolving threads, changing branches, and making follow-up issues are separate explicitly authorized operations.

## Source and documentation landmarks

Resolve `src\UnitTests.Shared\TaskEnvironmentHelper.cs` and `TestEnvironment.cs` in the reviewed checkout. Inspect relevant cases in `src\Build.UnitTests\BackEnd\TaskEnvironmentConstructorInjection_Tests.cs` and `TaskHost_MultiThreadableTask_Tests.cs` before adapting their host patterns. Existing tests are examples of assertions, not a claim that this audit executed them.

For command syntax, consult the current [MTP reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp) together with the actual SDK/runner help, not an unversioned command template.
