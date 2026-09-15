# Microsoft.Build.TaskCache

Optional runtime implementation for MSBuild's BuildXL-backed task cache, including
its managed dependencies and Windows, Linux, and macOS x64 native libraries.
Reference this package alongside `Microsoft.Build` when hosting cache-enabled
builds. The package exposes no compile-time API and does not enable caching:
hosts still opt in through `BuildParameters.TaskCache`.

The MSBuild CLI distribution supplies this runtime. Package build assets copy
RocksDB libraries into the location expected by its loader for build and publish.
