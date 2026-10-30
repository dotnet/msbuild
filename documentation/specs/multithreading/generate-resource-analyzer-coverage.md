# GenerateResource multithreading: analyzer coverage

This migration began by adding only `[MSBuildMultiThreadableTask]`, conditioned on
`!FEATURE_RESXREADER_LIVEDESERIALIZATION`, and building with the existing task
analyzers. The executable implementations are net11.0 and .NET Framework; only
the former receives the attribute. No analyzer implementation or suppression
was changed.

## Baseline and final findings

`src/Tasks/TaskAnalyzer.globalconfig` already enables MT analysis for all tasks.
Consequently, a Tasks project build also reports unrelated tasks. The build used
`BuildAnalyzer=true`, enabled build-time analyzers, treated warnings as errors,
and cleared the project's `WarningsNotAsErrors` exemptions.

| Unique errors | Attribute-only baseline | After migration |
| --- | ---: | ---: |
| `MSBuildTask0002` | 2 | 2 |
| `MSBuildTask0003` | 1 | 1 |
| `MSBuildTask0005` | 61 | 45 |
| Total | 64 | 48 |
| Findings naming `GenerateResource.Execute` | 16 | 0 |

Compiler output and its repeated build summary were deduplicated. The final
analyzer build still exits unsuccessfully because of the same 48 unrelated
errors; it is not a clean whole-project analyzer build.

All 16 GenerateResource baseline errors were `MSBuildTask0005`:

| Reported operation | Count | Resolution |
| --- | ---: | --- |
| `DependencyFile` constructor: `File.GetLastWriteTime` | 1 | Changed the constructor to require `AbsolutePath` for resource-cache entries. |
| `EnsurePathIsShortEnough`: three `Path.GetFullPath` calls and `Directory.CreateDirectory` | 4 | Excluded unsupported assembly-extraction output code from `FEATURE_ASSEMBLYLOADCONTEXT` builds. |
| `RemoveCorruptedFile`: `File.Delete` | 1 | Resolve through the task environment. |
| Minimal ResX parser: `XmlTextReader` constructor | 1 | Resolve through the task environment. |
| Binary output: two `FileStream` constructors | 2 | Resolve through the task environment. |
| Strongly typed output: `StreamWriter` | 1 | Resolve through the task environment. |
| Strongly typed warning: `Path.GetFullPath` | 1 | Canonicalize the task-relative path. |
| Text input: `File.Open` | 1 | Resolve through the task environment. |
| `PortableLibraryFile.AllOutputFilesAreUpToDate`: `FileInfo` | 1 | Accept and use the task environment. |
| `ResGenDependencies.UseSourcePath`: `Directory.GetCurrentDirectory` | 1 | Use the task's project directory when `UseSourcePath` is false. |
| Linked string resource: `StreamReader` | 1 | Resolve the linked file through the task environment. |
| `MSBuildResXReader`: input `FileStream` | 1 | Change the file-reading entry point to require an `AbsolutePath` filename and explicit absolute base directory. |

The four `EnsurePathIsShortEnough` reports describe a path that cannot execute on
net11.0: reading assembly resources is already rejected before the assembly
output branch is reached. The helper and that branch now share the existing
`!FEATURE_ASSEMBLYLOADCONTEXT` guard. This is path-insensitive reporting, not four
fixed runtime failures.

The constructor diagnostic's displayed chain also passed through assembly
extraction. However, the same string constructor was reachable through ordinary
`ResXFile` cache creation. That finding must not be dismissed merely because its
displayed shortest chain was infeasible.

## Work not surfaced by the baseline

These are observed coverage limits and supporting compatibility obligations, not
an assertion that every item needs a new analyzer rule.

| Additional work | Observation and evidence |
| --- | --- |
| Task-side existence checks and timestamp probes, including sources, outputs, strongly typed files, references, additional inputs, and linked dependencies | No GenerateResource baseline finding covered `FileSystems.Default` or `NativeMethodsShared` calls. Wrapper, external-compilation, and virtual-dispatch boundaries require manual inspection. `RelativeTextAndStronglyTypedOutputsUseProjectDirectory`, `AdditionalInputsUseProjectDirectory`, and `LinkedResourcesAndCacheUseProjectDirectory` check actual files and incremental decisions outside process CWD. |
| `DependencyFile.HasFileChanged` and text-output `FileUtilities.OpenWrite` | These wrapper-based I/O operations were not reported. They now receive resolved paths; resource transformation tests and incremental cache tests exercise the call sites. |
| Output duplicate identity | `FullPath` metadata already follows the engine's thread-local working directory and canonicalizes aliases. Duplicate detection uses it directly and retains the resolved path for timestamp checks while preserving the original item spelling. `DuplicateOutputsAreCanonicalized` covers relative aliases and a relative/absolute pair identifying the same project output. |
| State-file reads/writes, absolute resource-cache keys, and dependency filenames | No GenerateResource baseline finding covered this context propagation. `RelativeTextAndStronglyTypedOutputsUseProjectDirectory`, `LinkedResourcesAndCacheUseProjectDirectory`, and `InstancesWithSharedCacheRemainIndependent` verify cache files, invalidation, and distinct projects using the same relative source name. `AbsolutePortableLibraryCacheRoundTrips` checks that portable-library insertion, lookup, and serialization use the same absolute identity. |
| Linked byte-array and MemoryStream reads | `FileSystems.Default.ReadFileAllBytes` was not reported. `LinkedResourcesUseExplicitProjectDirectory` exercises both types using a project directory distinct from process CWD. |
| Delayed `FileStreamResource.AddTo` reads | Analysis did not follow `IResource.AddTo` dispatch to its eventual `FileStream` constructor. The resource now retains an `AbsolutePath`; the same reader theory serializes a delayed resource and verifies its payload while retaining the original linked-file and originating-file strings. |
| Diagnostic file paths and cleanup existence checks | `FileUtilities.GetFullPathNoThrow` and filesystem-wrapper uses were not reported. `MalformedInputReportsProjectFile`, `MalformedTextKeepsOriginalDiagnosticPath`, `MissingInputKeepsOriginalDiagnosticPath`, and `FailureCleanupUsesProjectDirectory` check reported paths and cleanup. The direct cleanup deletion was separately reported, as listed above. |
| TaskEnvironment injection into GenerateResource and its processor | Plumbing needed to resolve the reported operations; not a separate baseline diagnostic. Processor injection is excluded from AppDomain builds because `TaskEnvironment` cannot be marshaled across AppDomains. .NET Framework resource tests exercise the retained behavior. |
| Original output spelling, metadata, optional paths, and cache dirty-state reset | Compatibility obligations, not analyzer defects. Tests check relative `Sources`, `OutputResources`, `FilesWritten`, custom metadata, default/explicit strongly typed filenames, null/empty state-file inputs, and `AbsoluteStateFileResetsDirtyFlag`. The typed serialization override preserves the original dirty-state reset. |
| Feature-dependent routing | `AttributeMatchesResourceReaderCapabilities` runs on both target frameworks. .NET Framework remains unannotated even when `UsePreserializedResources` is true. |

Legacy live-deserialization, ResGen.exe, AppDomain, and assembly-reading branches
remain outside the annotated implementation. The remaining process-relative
operations in those branches were checked against their feature guards rather
than suppressed.

File-reading and cache helpers require `AbsolutePath` at their call boundaries;
callers resolve paths with their task environment rather than using redundant
string/fallback adapters. Environment-dependent helpers likewise require an
explicit `TaskEnvironment`. Both reader entry points require an explicit
absolute linked-file base directory; the string reader also accepts an optional
`AbsolutePath` filename. Parsing uses these paths directly, with no dependency
on `TaskEnvironment`. File-reading callers pass `TaskEnvironment.ProjectDirectory`;
string-reading tests supply their own absolute directory and optional filename.
Resource metadata and diagnostics use `filename.OriginalValue`. The original
source-relative versus project-relative resolution and delayed handling of empty
linked paths are preserved.

Dependency files retain `AbsolutePath` internally and check timestamps directly,
without re-resolving their filenames through `TaskEnvironment`. Cache translation
writes the absolute value as a string and validates it when reading. State-file
serialization version 7 invalidates older version-6 caches, including resource
entries that could contain relative filenames. This shared version bump also
invalidates other task state files once. Current-version resource entries with
relative filenames are rejected through the existing cache-read message path.
On .NET Framework, portable-library entries explicitly serialize their fields
and absolute filename for AppDomain transport; `AbsolutePath` itself does not
need to become serializable.

`RelativeDependencyCachesAreDiscarded` covers a version-6 cache containing a
relative filename and a malformed current-version entry.
`PortableLibraryAppDomainSerializationPreservesAbsolutePath` checks the .NET
Framework transport serialization.

GenerateResource reuses resolved paths within each processing stage. The state
file is resolved once for reading and writing; output duplicate validation
retains its paths for timestamp checks; and source existence, cache, timestamp,
and strongly typed checks share a resolved source path. The processor resolves
its input and output once and passes typed paths through readers, writers, and
failure cleanup. Diagnostics and resource origins retain `OriginalValue`.
Task-side checks and processor-side I/O remain separate resolution stages:
their .NET Framework boundary must still accept AppDomain-serializable strings
and task items, not `AbsolutePath`.

## Verification

The latest completed analyzer command was:

```powershell
dotnet msbuild src\Tasks\Microsoft.Build.Tasks.csproj -t:Rebuild -p:TargetFramework=net11.0 -p:BuildAnalyzer=true -p:RunAnalyzersDuringBuild=true -p:TreatWarningsAsErrors=true -p:WarningsNotAsErrors= -noconsolelogger "-bl:artifacts\log\resource-fullpath-metadata-analyzer-{}.binlog" "-flp:v=normal;LogFile=artifacts\log\generate-resource-fullpath-analyzer.log"
```

Result: the 48 unchanged, unrelated errors above; no GenerateResource findings,
compiler errors, or warnings. This includes the explicit reader APIs,
path-reuse refactor, and direct use of `FullPath` metadata.

The broader resource-and-cache test run used:

```powershell
dotnet test (Resolve-Path src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj).Path -c Release --no-restore "-bl:artifacts\log\resource-reused-paths-final-{}.binlog" -- --filter-class "*GenerateResource_Tests*" --filter-class "*MSBuildResXReader_Tests*" --filter-class "*ResGenDependencies_Tests*" --filter-class "*ResolveAssemblyReferenceCacheSerialization*" --filter-not-method "*ResgenCommandLineExceedsAllowedLength"
```

Result: compilation succeeded without warnings or errors; 329 tests passed,
14 were skipped, and none failed across net11.0 and .NET Framework.

After duplicate detection switched to using `FullPath` metadata directly, the
GenerateResource suite was rerun:

```powershell
dotnet test (Resolve-Path src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj).Path -c Release --no-restore "-bl:artifacts\log\resource-fullpath-metadata-{}.binlog" -- --filter-class "*GenerateResource_Tests*" --filter-not-method "*ResgenCommandLineExceedsAllowedLength"
```

Result: compilation succeeded without warnings or errors; 247 tests passed,
14 were skipped, and none failed across both target frameworks.

After both reader entry points switched to explicit `AbsolutePath` arguments,
the focused reader suite was rerun:

```powershell
dotnet test (Resolve-Path src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.csproj).Path -c Release --no-restore "-bl:artifacts\log\resource-reader-explicit-paths-{}.binlog" -- --filter-class "*MSBuildResXReader_Tests*"
```

Result: compilation succeeded without warnings or errors; all 62 tests passed
across net11.0 and .NET Framework, with none skipped or failed. The source-relative
and project-relative cases exercise both the file and string entry points.

An earlier temporary two-project resource build also ran through the already-built local
CLI with `-m:2 -mt -nr:false` and MSB4286/MSB4287/MSB4288 promoted to errors.
Both child projects used the same relative input, output, cache, and default
strongly typed filenames, but different `Greeting` values. Their shared target
invoked GenerateResource with `ExecuteAsTool=false` and
`StronglyTypedLanguage=CSharp`. The CLI's task assembly hash matched the tested
assembly. Both resource payloads, generated source files, and cache files were
verified. A second run with `FailIfNotIncremental=true` passed without rewriting
the resource outputs.

The successful strict and incremental binlogs contain only the expected
strict-mode enable message when searched for `MT-sentinel-CWD`; no task paths
leak the sentinel. Both GenerateResource invocations used the local task
assembly, not installed SDK tasks. Logs are retained under `artifacts\log`.

### Verification obstacles

- The initial relative-project multi-target test invocation built successfully
  but the test launcher duplicated the project path. Using an absolute project
  path fixed this. Class and method inclusion filters intersect; an earlier
  combination selected zero tests and was not counted as verification.
- An unexcluded resource-suite run failed in the pre-existing .NET Framework
  `ResgenCommandLineExceedsAllowedLength` test during `File.Delete` cleanup:
  the long worktree/output prefix plus its deliberately long filename exceeds
  the legacy path limit. That failure occurs outside task execution. The final
  command excludes only that method.
- A full-repository build was not authorized. The end-to-end check used the
  local CLI already produced by the focused build; no replacement full build
  was initiated.

These are validation/tooling limits, not analyzer defects. Analyzer fixes are
outside this migration's scope.
