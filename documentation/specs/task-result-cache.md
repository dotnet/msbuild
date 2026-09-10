# Declared-I/O task result cache

MSBuild can reuse successful task results from a content-addressed disk cache when `MSBuildTaskCacheDirectory` is nonempty or `MSBuildTaskCacheEnabled` is true. The feature is opt-in and does not change task execution when both properties are unset.

`MSBuildTaskCacheDirectory` selects an explicit cache location. When only `MSBuildTaskCacheEnabled` is true, MSBuild stores entries under `$XDG_CACHE_HOME/msbuild/task-result-cache` when `XDG_CACHE_HOME` is an absolute path, `~/.cache/msbuild/task-result-cache` on other Unix systems, or the local application-data directory on Windows. This persistent user cache survives clean builds and can be used by different branches that build from the same path. Cache keys currently include absolute project and declared-I/O paths, so moving a checkout to another fork or worktree shares the storage directory but not entries whose keys contain different paths.

A cacheable task type must have `Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute`. Each cacheable invocation must explicitly supply both `DeclaredInputs` and `DeclaredOutputs`, even when either list is empty, and must not contain MSBuild `<Output>` elements. A task can apply `MSBuildDeclaredIORequiresUnsetAttribute` to parameters whose behavior cannot satisfy its declared-I/O contract; invocations that bind those parameters execute normally without caching.

The cache key includes:

- The task type and task assembly contents.
- MSBuild, runtime, operating-system, architecture, and culture identity.
- The project path and execution directory.
- Every explicitly bound task parameter, including ordered task items and metadata.
- Declared output paths.
- The existence, type, and contents of each declared input.

Cache entries contain the declared output files and supported task messages and warnings. A hit restores outputs and replays diagnostics while retaining normal task start and finish events. Failed tasks, out-of-process tasks, directory outputs, unsupported task events, corrupt entries, and cache I/O failures fall back to ordinary execution.

Cache key hashing, entry-lock waits, payload validation and restoration, and cache storage use cancellation-aware asynchronous I/O. During a build, each node retains input-file digests and performs a fast length and last-write-time check on every cache request. Previously seen files with unchanged metadata reuse their digest; the first request for a new or changed file computes its digest while concurrent requests wait for that same operation. The digest table is discarded at build completion. Filesystem metadata operations without asynchronous platform APIs remain synchronous.

When a build runs in the resident MSBuild server, the server accumulates task-cache statistics in memory across builds. `-taskCacheStats` queries and prints the current server snapshot, and `-resetTaskCacheStats` resets it. No statistics file is written. Builds that do not use the resident server do not contribute to the cumulative snapshot.

The human-readable snapshot reports cache-enabled task requests, cache lookups, hits, misses, ineligible invocations, cache errors, successful stores, store errors, the cumulative hit rate, and average full-task latency for hits and misses. Hit timing includes lookup, output restoration, and event replay. Miss timing includes lookup, ordinary task execution, and cache storage. Worker nodes report one in-memory delta to the main node at build completion, avoiding persistent I/O on the task-execution hot path.

The cache remains outside the task implementation. On a miss, the task executes through its ordinary mechanism, including an out-of-process tool or compiler server when the task's declared-I/O contract permits it. Operational execution choices must not change the declared outputs, and every executable or assembly that can affect those outputs must participate in the declared inputs.

The cache does not discover hidden task inputs. The task implementation is responsible for declaring every filesystem dependency that can affect its outputs and for excluding invocation modes that depend on ambient or dynamically discovered state.
