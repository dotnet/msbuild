# BuildXL cache storage

Use BuildXL LocalCache for CAS and memoization while MSBuild owns execution,
fingerprints, and result replay. There is no alternative backend or cache daemon.

## Dependencies

Package-based API hosts reference `Microsoft.Build.TaskCache` alongside
`Microsoft.Build`. It carries the optional implementation and managed/native
runtime closure; ordinary engine consumers do not need cache-specific feeds.
Transitive package targets copy native files for both build and publish.

`Microsoft.BuildXL.Cache.MemoizationStore.Library` supplies LocalCache and
RocksDB memoization. `RocksDbNative` is referenced directly because its native
copy targets are not transitive. Dependency versions and feed mappings are
maintained in the repository's package configuration.

The packaged native assets cover Windows/Linux/macOS x64. Linux requires glibc
and libstdc++; runtime validation is currently Linux-only. Source-build excludes
the dependencies and consuming implementation through `FEATURE_BUILDXL_TASK_CACHE`.
Ordinary source-built MSBuild works; opting into task caching reports MSB1077.

## Layout and API boundary

Under the configured root:

```text
buildxl/
  owner.lock
  cache/
    memoization/
    ... BuildXL-managed content and metadata ...
```

The invocation key becomes a `StrongFingerprint` with a fixed empty-content
selector. `GetContentHashListAsync` looks up results;
`AddOrGetContentHashListAsync` publishes the manifest hash and artifact hashes.
`PutFileAsync` hashes/copies artifacts; `PutStreamAsync` stores manifests.
Each publication has its own artifact list. Copy insertion and private output
staging prevent writable inode sharing with CAS content.

One coordinator-owned cache/session holds exclusive directory ownership for the
top-level build. Acquisition is fail-fast. Workers use the existing coordinator
connection rather than opening the database themselves. Concurrent operations
do not serialize task execution. Shutdown drains calls, closes the session/cache,
then releases ownership. A leftover lock file is not a held OS lock.

## Retention and maintenance

The build-long `ImplicitPin.PutAndGet` session protects content until shutdown.
Lookup explicitly pins all referenced hashes; already-evicted content is a miss.
BuildXL enforces a 10 GiB content quota. Pinned content can exhaust that quota;
insertion failures are build errors, not silent cache bypass.

At cache startup, run metadata GC if the last successful collection is at least
an hour old or its recorded time is in the future. Keep the timestamp in
`owner.lock`; do not run a periodic GC timer during the build.
The 64 MB metadata target is size-driven with older last-access records as
eviction candidates, not a TTL. Metadata removal and CAS eviction are separate.
Database bookkeeping means these limits do not cap total directory size.

## Bootstrap

`AddBootstrapTaskCacheDependencies` merges the cache dependency closure into
the bootstrap SDK's runtime manifests and copies required assets, preserving
existing SDK package versions. Baseline manifests allow replacement of cache
additions on subsequent builds. This integration is excluded from source-build.
