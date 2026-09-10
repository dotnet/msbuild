# Controlled MSBuild experiment

## Experiment record

Fill this before running measurement rounds; discover values rather than copying a past session's machine state.

| Field | Required distinction |
|---|---|
| Question | Full SDK comparison, MSBuild-only revision change, or one execution-mode change |
| A/B identity | Absolute host paths, SDK/product versions, source SHA when relevant, architecture/runtime |
| Workload | Exact project/solution/input revision; synthetic versus representative |
| Mode | Default, `-m`, or `-mt`; relevant node reuse/server/compiler-server settings |
| Build case | Clean outputs, controlled source-edit incremental, or no-op |
| Warmth | MSBuild process/server, compiler process/server, filesystem cache, restore/package state |
| Constants | Configuration, targets, properties, restore policy, logging/instrumentation, environment |
| Plan | Warm-up, reset procedure, paired ordering, round budget, outlier policy, stopping condition |
| Evidence | Per-round timings/results plus commands and diagnostic artifacts |

A clean build means outputs were reset; it does not imply cold processes or cold filesystem caches. A source-edit incremental build is not a no-op. Warm compiler and MSBuild servers are distinct.

Comparing two SDKs also changes their compilers/tasks/dependencies. Label that a full SDK comparison unless those variables are controlled; do not attribute every difference to MSBuild alone.

## Preflight and provenance

Resolve and record the actual executable paths. Check SDK selection from the workload directory, including `global.json` and environment overrides. Separate baseline/candidate outputs so a build cannot overwrite the other side.

Run each case once for correctness before timing. Establish requested mode versus actual execution using relevant process and binlog evidence; a switch can be rejected, forwarded differently by a `dotnet` verb, or cause fallback.

For local revisions, use the [bootstrap workflow](../../use-bootstrap-msbuild/SKILL.md). For installed SDKs, do not silently replace them with local bootstrap bits or modify the repository's SDK pin.

## Rounds and artifacts

Use paired interleaved ordering, such as A/B then B/A, to reduce time-dependent machine drift. Declare the number of rounds and how resets/warm-up work before interpreting results.

Keep both sides' logging identical. If collecting a binlog materially perturbs the timing question, use separate diagnostic runs and state that distinction. Record failures instead of dropping them silently.

Capture a machine-readable per-round record with case, order, identity, elapsed time, result, and relevant process state. Keep raw artifacts outside committed product source unless requested.

Report the center and spread of timings plus paired differences. If noise is larger than the effect, say the experiment is inconclusive. Do not report only a favorable minimum or remove outliers after seeing which side benefits.

## Investigate only the observed difference

Use available binlog tools to narrow evaluation, targets/tasks, parallelism, task hosts, and critical-path changes. Profile CPU/allocations/I/O only when needed for the causal question. Read the relevant [performance guidance](../../optimizing-msbuild-performance/SKILL.md) rather than loading every performance skill.

A different process count or target duration supports a mechanism but does not alone prove an end-to-end improvement. Verify that the workload still produced equivalent intended outputs.

## Boundaries

Do not disable security software, clear machine-wide caches, or terminate unrelated processes. Identify only task-owned processes by PID. A busy shared machine is a limitation to record, not permission to reconfigure it.

Use the planned budget; extend it only for a stated reason or user request. Avoid repeated trials until a positive result or an arbitrary significance threshold appears.
