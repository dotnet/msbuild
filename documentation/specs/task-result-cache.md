# Declared-I/O task result cache

MSBuild can reuse successful task results from a content-addressed disk cache when `MSBuildTaskCacheDirectory` is nonempty. The feature is opt-in and does not change task execution when the property is unset.

A cacheable task type must have `Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute`. Each cacheable invocation must explicitly supply both `DeclaredInputs` and `DeclaredOutputs`, even when either list is empty, and must not contain MSBuild `<Output>` elements. A task can apply `MSBuildDeclaredIORequiresUnsetAttribute` to parameters whose behavior cannot satisfy its declared-I/O contract; invocations that bind those parameters execute normally without caching.

The cache key includes:

- The task type and task assembly contents.
- MSBuild, runtime, operating-system, architecture, and culture identity.
- The project path and execution directory.
- Every explicitly bound task parameter, including ordered task items and metadata.
- Declared output paths.
- The existence, type, and contents of each declared input.

Cache entries contain the declared output files and supported task messages and warnings. A hit restores outputs and replays diagnostics while retaining normal task start and finish events. Failed tasks, out-of-process tasks, directory outputs, unsupported task events, corrupt entries, and cache I/O failures fall back to ordinary execution.

The cache does not discover hidden task inputs. The task implementation is responsible for declaring every filesystem dependency that can affect its outputs and for excluding invocation modes that depend on ambient or dynamically discovered state.
