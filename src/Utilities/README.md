# Microsoft.Build.Utilities.Core

This package contains `Microsoft.Build.Utilities.Core.dll`, which defines helper functionality for MSBuild extenders, including

* [`Task`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.task), a base class for custom tasks,
* [`ToolTask`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.tooltask), a base class for tasks that run a command-line tool, and
* [`Logger`](https://docs.microsoft.com/dotnet/api/microsoft.build.utilities.logger), a base class for custom logging functionality.

### netstandard2.0 target
The `netstandard2.0` target of this build is configured only to output reference assemblies; at runtime MSBuild will be `net11.0` or `net472`. Please use the `net11.0`-targeted assemblies for .NET 11+ scenarios.

For context, see https://github.com/dotnet/msbuild/pull/6148

### Experimental persistent cache

`PersistentCache`, `PersistentCacheInputs`, and `PersistentCacheOutputs` provide local
directory/file-set caching, ported from the [shared invocation cache in
dotnet/runtime](https://github.com/dotnet/runtime/tree/fc5e8a9fd63f0acba1447bdfbd72c1d822eb5133/src/tasks/Common/InvocationCache).
All three types have `[Experimental("MSBUILD001")]`; consumers must explicitly suppress
that diagnostic to opt in. Neither the API nor the on-disk format has a compatibility guarantee.
Existing tasks and builds are unchanged.

```csharp
#pragma warning disable MSBUILD001

var cache = new PersistentCache(cacheDirectory);
var inputs = PersistentCacheInputs.FromFiles(inputAssembly, toolAssembly, taskAssembly);
var outputs = PersistentCacheOutputs.FromDirectory(outputDirectory);
string key = PersistentCache.ComputeKey(inputs, outputs, effectiveArguments);
DateTime timestamp = DateTime.UtcNow;

if (!cache.TryRestore(key, outputs, timestamp))
{
    PrepareOutputs();
    int exitCode = RunTool();
    if (exitCode == 0 && !Log.HasLoggedErrors)
    {
        cache.TryStore(key, outputs);
    }
}

#pragma warning restore MSBUILD001
```

The caller inventories every relevant input and toolchain file. Key computation hashes file
contents with SHA-256 and includes ordered absolute paths, missing-input markers, output paths,
explicit context values, the current directory, OS, process architecture, and cultures.
Pass effective arguments and relevant environment settings as context; dependencies are not
discovered automatically. Compute the key once before execution and keep inputs stable through
lookup, execution, and storage. Input read errors other than missing files propagate, so callers
that want to bypass caching on such errors must handle them.

An owned output directory is replaced completely on a hit. For individual files, use
`PersistentCacheOutputs.FromFiles(...)`: order is significant, unrelated files are preserved,
and files recorded as absent are deleted on restore. Output paths must not overlap each other
or the cache directory. Invalid declarations throw before cache I/O or output cleanup.

Entries have checksummed manifests and file contents, and are staged and atomically published.
`TryStore` returns false for existing entries, contending writers, or recoverable storage failures.
`TryRestore` returns false for misses, invalid entries, or recoverable restore failures.
These methods do not log diagnostics or run tasks. Restore validates the entire entry before
changing outputs, but copying is not transactional: normal execution must clean up stale or
partially restored outputs. Store only successful results with no logged errors.

Use a trusted local cache directory. Symbolic links and filesystem aliases are not checked;
empty directories, permissions, and other file metadata are not preserved. Concurrent local
processes may share entries but must use distinct output destinations. Distributed writers,
automatic eviction, cancellation, diagnostic replay, and caching non-file task outputs are not
provided. Do not delete entries while the cache is in use; corrupt entries are not repaired.

Task input/output annotations, sandbox enforcement, and a BuildXL backend are separate work
and are not part of this API.
