# Task briefs and context regression scenarios

These are copy/paste examples and maintenance scenarios, not automatically discovered CLI prompt commands. Fill in the placeholders; do not paste private logs or credentials into public artifacts.

## Review with proof, not repeated reassurance

> Review `<repo/PR or local diff>` against `<base>` at `<head>`, read-only. First map the intended behavior and supported configurations. For each potential blocker, show the source trace or a reproducible failing scenario; distinguish regression evidence from missing coverage. Produce one concise table of confirmed defects and a short list of unverified boundaries. Do not post, fix, or launch one agent per review dimension.

Expected route: review entry point -> relevant review lens -> changed implementation and callers -> targeted reproduction if needed. A no-findings result must not become "all possible cases proved correct."

## Compare the intended build experiment

> Compare `<SDK/build A>` and `<SDK/build B>` on `<workload>`, with `<default/-m/-mt>` explicit. Measure `<clean/source-edit incremental/no-op>` and `<cold/warm>` as separate cases. Identify executable paths and prove the expected server/task-host behavior before timing. Keep commands and workload identical except for the chosen variable, use paired interleaved rounds, and return measurements plus uncertainty and diagnostic artifacts.

Expected route: benchmark entry point -> experiment preflight -> measurements -> diagnostics only for an observed difference. Clean outputs, a warm MSBuild server, a warm compiler server, and warm filesystem caches are different conditions; none should be inferred from another.

## Fix a regression in the actual product

> Reproduce `<issue>` using `<host/runtime/mode>`. Pin the failing baseline and candidate fix. Use the same minimal project and assertion on both, and show which binaries were executed. Make the local fix only after the failure is understood; do not change SDK/feed configuration or publish anything.

Expected route: bootstrap reproduction -> source identity -> baseline/fix comparison. Passing tests against an installed unrelated MSBuild is not evidence about the edited source.

For a TaskHost symptom, consult the [host map](../../.github/skills/use-bootstrap-msbuild/references/host-map.md) before selecting a project. Modern MSBuild `/nodemode:2` must not be confused with the separate legacy `MSBuildTaskHost.exe`.

## Update documentation without rewriting its contract

> Update the authoring source for `<topic>`, for `<public API users / migration authors / management>`. Preserve existing useful examples and detailed internal guidance. Add only `<specific semantic clarification>`. First identify which repositories/files are authoritative and which are generated mirrors. Return the smallest accurate change; no external PR or comment until requested.

Expected route: source discovery -> implementation contract -> scoped doc edit. A review consideration is not automatically a warning, an implementation detail is not automatically a public guarantee, and shorter public prose must not erase agent reference material.

## Handle a changed PR base

> The target base is now `<branch>`. Before continuing, refresh base/head identity, inspect the actual PR diff for unrelated commits, and identify which earlier conclusions or artifacts are invalidated. Do not rewrite history unless I request it.

Expected route: identity preflight before more code work. Knowledge of the old checkout must not substitute for source at the new base.

## Triage CI without treating a retry as a fix

> Inspect `<PR/build/leg>`, read-only. Distinguish product failure, test flake, infrastructure failure, and unavailable evidence. Retrieve only the failing leg's relevant logs initially. State what a retry could establish and what it cannot; do not retry, quarantine, merge, or post.

Expected route: high-level status -> one failing boundary -> relevant CI/Helix/binlog specialist if available. Missing access is unknown status, not green. A successful retry does not prove a deterministic defect was fixed.

## Test a small change without changing test isolation

> Run the existing tests covering `<behavior>` in `<project>`. Determine the SDK, test runner, and applicable TFM from this checkout, use a targeted filter, and confirm it actually selects tests. Preserve parallelism, traits, and quarantine settings. Escalate only for an uncovered boundary or a failure.

Expected route: local test entry point -> current runner syntax -> scoped run. It must not activate a large test-generation pipeline, rewrite runner settings, or require a full product build for Markdown edits.

## Distinguish task intent from capability keywords

> Audit these skills and instructions for stale context and unclear routing.

Expected route: context maintenance guide and the files being audited. Mentioning agents is not a request to configure cloud-agent runners; mentioning tests is not a request to generate tests; a PR URL is not permission to publish a review.
