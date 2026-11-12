# Content cache for declared-I/O tasks

MSBuild can reuse successful task results from a disk cache keyed by task inputs when `MSBuildContentCacheDirectory` is nonempty or `MSBuildContentCacheEnabled` is true. The feature is opt-in and does not change task execution when both properties are unset.

`MSBuildContentCacheDirectory` selects an explicit cache location. When only `MSBuildContentCacheEnabled` is true, MSBuild stores entries under `$XDG_CACHE_HOME/msbuild/content-cache` when `XDG_CACHE_HOME` is an absolute path, `~/.cache/msbuild/content-cache` on other Unix systems, or the local application-data directory on Windows. This persistent user cache survives clean builds and can be used by different branches that build from the same path. Cache keys currently include absolute project and declared-I/O paths, so moving a checkout to another fork or worktree shares the storage directory but not entries whose keys contain different paths.

The cache directory must be trusted: a cache entry's manifest supplies the paths that restore may write or delete. A user who can modify cache entries can affect files accessible to the build.

`MSBuildContentCacheSizeMB` controls the maximum size of live cache entries, where one MB is 1,048,576 bytes. The default is 10,000 MB. A value of zero disables eviction. MSBuild updates an entry's recency after a successful hit and periodically removes least-recently-used entries when the cache exceeds the configured size.

A cacheable task type must have the experimental `Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute` and publicly readable `IEnumerable<string>` properties named `InputFiles` and `OutputFiles` without public setters. These properties are task-computed conventions: the engine reads `InputFiles` after parameter binding and `OutputFiles` after successful execution, and an invocation that supplies either as a task parameter is not cached. `OutputFiles` must include every path whose final presence or absence is part of the task result. Cacheable invocations must not contain MSBuild `<Output>` elements. Every explicitly bound task parameter is part of the key. Opting in requires every supported invocation to be fully determined by its parameters, declared inputs, and the other values in the cache key.

The cache key includes:

- The task type and task assembly contents.
- The MSBuild engine module, runtime, operating-system, architecture, and culture identity.
- The project path and execution directory.
- Every explicitly bound task parameter, including ordered task items and metadata.
- The existence, type, and contents of each declared input.

Cache entries contain the task-reported output paths and files plus supported task messages and warnings. A hit restores outputs and replays diagnostics while retaining normal task start and finish events. Failed tasks, out-of-process tasks, directory outputs, unsupported task events, corrupt entries, and cache I/O failures fall back to ordinary execution.

Cache key hashing and manifest I/O use cancellation-aware asynchronous I/O. Payloads are hashed while being copied and validated by length and SHA-256 digest before staged files replace declared outputs. Existing outputs are preserved until the staged set is ready, and a failed replacement restores the prior output state before normal task execution. During a build, each node retains file digests and performs a fast length and last-write-time check on every cache request. Previously seen files with unchanged metadata reuse their digest; stored outputs populate this table, and restored outputs seed it for downstream consumers. The first request for a new or changed file computes its digest while concurrent requests wait for that same operation. The digest table is discarded at build completion. Filesystem operations without asynchronous platform APIs remain synchronous.

Content-cache hits, misses, and failures are logged at low importance and captured in binary logs.

The cache remains outside the task implementation. On a miss, the task executes through its ordinary mechanism, including an out-of-process tool or compiler server when the task's declared-I/O contract permits it. Operational execution choices must not change the declared outputs, and every executable or assembly that can affect those outputs must participate in the declared inputs.

On .NET Framework, an AppDomain-isolated task may fall back to ordinary execution if its file-list property value cannot be marshalled across the AppDomain boundary.

The cache does not discover hidden task inputs. The task implementation is responsible for declaring every filesystem dependency that can affect its outputs. A task with supported invocation modes that depend on untracked ambient state must not opt in; a separate task with a restricted surface can opt in instead.
