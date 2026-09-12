# Task-cache design decisions

MSBuild owns task execution and result semantics. BuildXL supplies content
storage and memoization, not execution or dependency discovery.

## Scope and configuration

- Cache individual task invocations. Preserve target timestamp skipping and
  output inference; skipped targets do not consult the task cache.
- Require `-taskCache` or `BuildParameters.TaskCache`. Configuring a directory
  alone enables nothing.
- Configure the shared storage root through `-buildCacheDirectory:<path>` or
  `BuildParameters.BuildCacheDirectory`. Resolve it once for the top-level build.
  Project properties and environment variables cannot override it.
- Use platform-default user cache directories when configuration is unspecified.
  Do not expose the directory as a project property.
- Use only BuildXL storage. Do not add a runner, persistent service, sandbox,
  access monitoring, learned dependencies, or a separate timing facility.

## Task contract

- Describe file I/O with unconditional class-level annotations naming task
  parameters. Inputs and outputs must be disjoint within an invocation.
- Trust the author to provide complete conservative dependencies and deterministic
  behavior. Structural validation cannot prove the absence of hidden effects.
- Resolve the requested task normally and inspect its actual type. Never
  substitute tasks to make an invocation cacheable.
- Use a separate restricted task when an existing task's parameter surface
  cannot meet the contract. Keep existing invocations unchanged.
- Allow conservative output-path supersets. Record whether each candidate exists
  after execution, including outputs removed or not produced.

## Keys and results

- Hash task implementation, bound inputs, execution context/environment, declared
  input contents and absence, output paths, and referenced output-parameter names.
  Workers compute input hashes; revalidate inputs before publication.
- Include engine `ModuleVersionId` in every key. This isolates serialization
  changes between engine binaries, so the cache format has no separate version
  fields or migration promise.
- Reuse `TaskParameter` and `BinaryTranslator` for typed values and item metadata.
  Keep one manifest per invocation, containing file records, output values,
  and supported warnings. See the [format reference](task-cache.md).
- Capture every referenced output getter after successful execution, regardless
  of binding conditions. Getters must be safe to read then. Read each once before
  cleanup; publish afterward so cleanup failures cannot produce a reusable result.
- Keep binding conditions and destination property/item names outside the cached
  result. Apply current bindings to independent restored values.

## Ownership and publication

- One coordinator owns LocalCache and its session for the top-level build.
  Acquire directory ownership without waiting; competing builds fail.
  Ownership remains held while tools execute, without serializing task execution.
- Workers access the owner through existing node communication. Exchange paths,
  hashes, and result metadata, not artifact contents through in-memory transport.
- Keep session pins for the build. Await each publication in the task path;
  do not add a background publication queue.
- Let CAS hash output files during insertion. Use the returned hashes rather
  than prehashing and rereading the same outputs in MSBuild.
- At shutdown, stop new operations, cancel active calls, and await completion
  before closing storage and releasing ownership. Do not abandon active calls
  through a separate shutdown timeout.

## Diagnostics and failure recovery

- Cache standard warnings from execution and output getters before warning-policy
  conversion. Replay them in the current task context under current warning policy.
- Setup/cleanup diagnostics and unsupported warning subclasses prevent caching
  rather than being replayed incompletely or twice. Raw errors, failed tasks,
  exceptions, and cancellation prevent publication. Do not replay ordinary messages.
- Missing entries or evicted content are misses. Corruption and operational
  failures fail the build, without task-execution fallback.
- Validate and stage all artifacts before replacing destinations. Honor
  cancellation before replacement, then finish replacement without cancellation
  interruption. Filesystem errors remain fatal.
- Do not implement multi-file rollback. Preserve the primary error during cleanup
  and require clean/rebuild recovery after partial restoration; timestamp skipping
  prevents a guarantee of automatic incremental recovery.

## Storage policy

Use a 10 GiB content quota and build-long pins. Check metadata collection only
when opening the cache, with a one-hour minimum interval and a 64 MB size target.
Metadata eviction is based on size and last access, not a fixed expiration age.

Exclude BuildXL dependencies and implementation from source-build. Permit the
packaged Windows/Linux/macOS x64 assets; runtime validation is currently Linux-only.
See the [storage reference](task-cache-buildxl.md).
