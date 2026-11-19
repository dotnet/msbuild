# Microsoft.Build.Utilities.Core

This package contains `Microsoft.Build.Utilities.Core.dll`, which defines helper functionality for MSBuild extenders, including

* [`Task`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.task), a base class for custom tasks,
* [`ToolTask`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.tooltask), a base class for tasks that run a command-line tool, and
* [`Logger`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.logger), a base class for custom logging functionality.

### netstandard2.0 target
The `netstandard2.0` target of this build is configured only to output reference assemblies; at runtime MSBuild will be `net11.0` or `net472`. Please use the `net11.0`-targeted assemblies for .NET 11+ scenarios.

For context, see https://github.com/dotnet/msbuild/pull/6148

### Experimental invocation cache

`InvocationCache` and `CachedToolTask` are ported from the [shared invocation cache used by
ILLink in dotnet/runtime](https://github.com/dotnet/runtime/tree/ab27f20941c/src/tasks/Common/InvocationCache).
The port preserves runtime's implementation, including keys, storage format, writer locks,
diagnostics, environment configuration, fallback, and purge behavior. The namespace is
`Microsoft.Build.Utilities`; types needed by consumers are public, and the two
`CachedToolTask` discovery hooks are protected rather than private protected.
Public cache types have `[Experimental("MSBUILD001")]`. Neither the API nor the storage
format has a compatibility guarantee. Existing tasks and builds are unchanged.

Derive from `CachedToolTask` and override `CacheConfiguration` and `DescribeInvocation`.
Override `ExecuteToolCore` if normal execution needs tool-specific output preparation.
Only `true` in the configured enablement environment variable enables caching; a path
alone does not. Without a path override, storage uses the platform's user cache directory.
Disabled caching skips discovery and cache I/O. Return null from `DescribeInvocation`
for unsupported invocations.

For callers that own execution and diagnostic replay directly:

```csharp
#pragma warning disable MSBUILD001

InvocationCache? cache = InvocationCache.TryCreateFromEnvironment(Log, configuration);

InvocationResult ExecuteAndCapture()
{
    PrepareOutputs();
    return RunToolWithRawOutputCapture();
}

if (cache is not null)
{
    var invocation = new InvocationDescription(
        InvocationInputs.FromFiles(inputAssembly, toolAssembly, taskAssembly),
        [effectiveArguments],
        InvocationOutputs.FromDirectory(outputDirectory));
    if (cache.TryExecuteCached(invocation, ExecuteAndCapture, out InvocationResult result))
    {
        ReplayUsingCurrentLoggingPolicy(result.StandardOutput, result.StandardError);
        return result.ExitCode == 0 && !Log.HasLoggedErrors;
    }
}

return ExecuteNormally();

#pragma warning restore MSBUILD001
```

The caller inventories every relevant input and toolchain file. Key computation hashes file
contents with SHA-256 and includes ordered absolute paths, missing-input markers, output paths,
explicit context values, the current directory, OS, process architecture, and cultures.
Pass effective arguments and relevant environment settings as context; dependencies are not
discovered automatically. Keep inputs stable through discovery, hashing, and execution.
`TryExecuteCached` returns false without executing on recoverable input-identity failures.
Otherwise it returns the restored result or invokes the callback once. Only exit code zero
is stored, independently of logged errors. Execution and replay exceptions must not retry the tool.

An owned output directory is replaced completely on a hit. For individual files, use
`InvocationOutputs.FromFiles(...)`: order is significant, unrelated files are preserved,
and files recorded as absent are deleted on restore. Output paths must not overlap each other
or the cache directory. Invalid declarations throw before cache I/O or output cleanup.

Entries have checksummed manifests, file contents, and diagnostics, and are staged and atomically
published. Recoverable cache failures are logged and fall back to normal execution.
Restore validates the entire entry before changing outputs, but copying is not transactional:
normal execution must clean up stale or partially restored outputs. The cache reconciles
outputs only on hits, including deleting declared files recorded as absent.

`InvocationResult` contains the tool exit code and separate raw stdout/stderr sequences.
The cache does not replay them. `CachedToolTask` captures eligible misses without live
tool-output logging, then replays both misses and hits through current `ToolTask` policy:
stdout first, then stderr, preserving line order within each stream. Bypasses keep live logging.
Capture buffers are allocated only for eligible misses and released even when execution throws;
partial output is replayed on exceptions. Logging errors fail the task but do not prevent
storing a zero-exit tool result. Other non-file task outputs are not restored.

Use a trusted local cache directory. Symbolic links and filesystem aliases are not checked;
empty directories, permissions, and other file metadata are not preserved. Concurrent local
processes may share entries but must use distinct output destinations. Distributed writers,
automatic eviction, and cancellation are not provided. Keys retain absolute paths, so
cross-worktree reuse is not promised. Inherited environment settings and installed runtime trees
are not automatically fingerprinted. Consumers must bypass invocations whose dependencies or
required side effects cannot be modeled.

`InvocationCachePurge.Run` removes entries older than a last-use cutoff within the supplied
format directory (currently `v1`). It falls back to directory creation time for missing or
invalid markers. Do not purge or manually delete entries while the cache is in use.
Corrupt entries are not repaired. Entries from the older storage-only `PersistentCache`
prototype lack diagnostics and are rejected rather than treated as warning-free.

Task input/output annotations, sandbox enforcement, and a BuildXL backend are separate work
and are not part of this API.
