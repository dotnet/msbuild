# `-mt` build-equivalence findings

Results of running MSBuild's official-style build with and without `-mt` and comparing the outputs
byte for byte. The harness that produces these results is
[`scripts/mt-equivalence`](../../scripts/mt-equivalence/README.md); the pipeline is
[`azure-pipelines/.vsts-dotnet-mt-equivalence.yml`](../../azure-pipelines/.vsts-dotnet-mt-equivalence.yml).

## Method

Three official-style builds of the same commit, in the same working tree, differing only in whether
`MSBUILD_MT_ENABLED=1` was set (which is how `eng/common/tools.ps1` adds `-mt` to the MSBuild command
line):

```
build.cmd -ci -pack -configuration Release
          /p:OfficialBuildId=<fixed> /p:RepositoryName=dotnet/msbuild /p:TeamName=MSBuild
          /p:DotNetPublishUsingPipelines=true /p:SuppressFinalPackageVersion=true /p:IsExperimental=true
```

`-sign` from the official job is omitted: it needs the MicroBuild signing plugin, and an Authenticode
signature embeds a trusted-timestamp countersignature, so signed binaries could never be byte-identical
between two runs. What `-mt` could actually affect is the content being signed, and that is compared
unsigned. `EnableNgenOptimization` and `GenerateSbom` are turned off (the former is the official
pipeline's own `$(SkipApplyOptimizationDataArg)`; the latter produces manifests that embed a timestamp
and a per-run GUID). `VisualStudioDropName` is supplied, exactly as the official job does, because
`AfterSigning.proj` requires it to generate the VS insertion manifests.

`-publish` **is** passed. It is the official-build publish, not `dotnet publish`: with
`DotNetPublishUsingPipelines=true` it pushes nothing to any feed (it emits `##vso[artifact.upload]`
logging commands; feed publishing happens in the separate post-build stage) but it does produce the
Build Asset Registry manifest, the generated symbol packages and the PDBs staged for the symbol
server. All three are compared.

The engine is `vs`, which is what the official job uses. This matters: only the `vs` engine builds the
**VS insertion outputs** — `artifacts/VSSetup` (`.vsix`, `.vsman`, `.vsmand`), the
`VS.ExternalAPIs.MSBuild` package and the arm64 flavours. With `-publish` the full official-style
build produces 28 061 artifact files, versus 23 329 for a `-msbuildEngine dotnet` build without
`-publish`, so validating that way would silently skip both the insertion surface and the publish
outputs.

The third build (`control`) is a second non-`-mt` build. It establishes how much this repo's output
varies between two *identical* builds, so a difference in the `-mt` comparison can be attributed.

That the `-mt` build really ran multithreaded is verified from the MSBuild command line recorded in
its binary log, not from the fact that the environment variable was set.

Runs: `main` at `867c136`, `Release`. Several trios were run while developing the harness; the
authoritative one is the full official configuration (`-MSBuildEngine vs`, `-publish`) executed on the
official MicroBuild pool by the pipeline itself — DevDiv build
[14817031](https://devdiv.visualstudio.com/DevDiv/_build/results?buildId=14817031&view=results), which
passed end to end. Earlier `-msbuildEngine dotnet` trios and several earlier pool runs agreed with it.

## Headline result

**`-mt` changed nothing in the produced bits**, including every VS insertion output, the Build Asset
Registry manifest, all staged PDBs and all symbol-package payloads.

| comparison | paths compared | byte-identical | unexpected differences | expected differences |
|---|---|---|---|---|
| `mt` vs `baseline` | 28 149 | 27 997 | **0** | 152 |
| `control` vs `baseline` | 28 149 | 28 000 | **0** | 149 |

Only 18 paths are excluded from the comparison altogether, and the report names every one of them:
14 Guardian SARIF files injected by the 1ES SDL template, 2 developer-convenience environment
scripts, 1 shortcut and 1 build log. Everything else in the tree is compared.

The three builds are also structurally identical, read from the binlog event stream rather than from
rendered text: 384 distinct targets over 18 993 executions, 9 306 distinct `(project, target)` pairs,
122 tasks over 16 083 invocations, 53 projects over 1 842 builds, and no warnings or errors — every
count equal across `baseline`, `mt` and `control`.

That the `-mt` build really ran multithreaded is asserted from the recorded command line, not assumed:

```
[mt-vs-baseline] baseline -mt = False / candidate -mt = True
[control]        baseline -mt = False / candidate -mt = False
```

The harness was also shown to be capable of failing: removing the known-`-mt` log allowance surfaced
the real `-mt`-only lines (exit 1); flipping a single byte in `Microsoft.Build.dll` in the `mt`
snapshot was reported as `PE: 1 bytes, regions: .text@0x4000+1` (exit 1); appending one byte to
`manifest.json` inside `Microsoft.Build.vsix` — the one place the comparison is deliberately relaxed —
was still caught and named (exit 1); a target that executes but logs nothing was invisible to the text
comparison and caught by the structural one (exit 1); and presenting a non-`-mt` binlog as the `-mt`
candidate was rejected by the evidence guard (exit 1). Corrupting the *control* snapshot instead
produced a warning and exit 0, so pre-existing non-determinism cannot masquerade as an `-mt`
regression.

Every file that differs between the `-mt` build and the baseline also differs between the two
identical non-`-mt` builds, and in the same way.

There were no errors or warnings in any of the three builds, and the sets of executed tasks and of
task→assembly bindings were identical between `-mt` and baseline.

## Pre-existing build non-determinism (not caused by `-mt`)

These show up in the non-`-mt` control run as well. They are classified as expected by
[`ArtifactCompareRules.json`](../../scripts/mt-equivalence/ArtifactCompareRules.json).

### 1. Three test projects are compiled with `/deterministic-`

`Microsoft.Build.CommandLine.UnitTests`, `Microsoft.Build.Engine.UnitTests` and
`Microsoft.Build.Tasks.UnitTests` are compiled with `/deterministic-`, so each build stamps a fresh
random v4 GUID as the assembly MVID. Two identical builds therefore produce assemblies that differ in
exactly 17-18 bytes: the 16-byte MVID in the `#GUID` metadata heap plus the PE COFF `TimeDateStamp`
(which Roslyn derives from the MVID).

```
bin/Microsoft.Build.CommandLine.UnitTests/Release/net11.0/Microsoft.Build.CommandLine.UnitTests.dll
  run A @0x247B4: 51 FB 0A F9 E9 25 B2 4D A5 47 84 6A F6 79 2B 69
  run B @0x247B4: 85 58 39 54 D3 8C 58 48 97 DB EE 2D 3C 3E 84 05     (both v4 = random)
```

Verified from the `Csc` command line in the binary log (`/deterministic-` for these projects, and the
`Deterministic` property evaluates to `true` at evaluation time but reaches the `Csc` task as
`False`). This affects 12 paths (6 assemblies × `bin` and `obj`).

**Impact:** none on shipping bits — no product assembly is affected — but it does mean these test
assemblies are not reproducible. Worth fixing separately; it is unrelated to `-mt`.

The harness does not hard-code these project names. It detects the shape of the difference: if the
only differing bytes are the `TimeDateStamp`, the `CheckSum` and a single 16-byte window, the
assembly was built non-deterministically, because under `/deterministic+` the MVID is a hash of the
emitted content and cannot differ on its own.

### 2. MSBuild's `.vsix` — and therefore its VS insertion manifests — are not reproducible

Every `.vsix` differs between two identical builds. The cause is a single attribute:

```xml
<!-- _rels/.rels, build A -->
<Relationship Type="PackageRelationshipType" Target="/manifest.json" Id="R96d7ed6e97444f81" />
<!-- _rels/.rels, build B -->
<Relationship Type="PackageRelationshipType" Target="/manifest.json" Id="Rab70d89c196a46c4" />
```

`System.IO.Packaging` generates a fresh random OPC relationship id on every save, in
`_rels/.rels` and `_rels/manifest.json.rels`. Everything else in the package is byte-identical
(verified per entry). Because the `.vsix` bytes change, so does its size and SHA256, and that
cascades into every artifact derived from it:

| file | what it inherits |
|---|---|
| `VSSetup/Release/Insertion/Microsoft.Build{,.Arm64}.vsman` | the vsix `sha256` and `size` |
| `…/Microsoft.Build{,.Arm64}.vsmand` | compressed form of the above |
| `…/Microsoft.Build{,.Arm64}.json` | the vsix `size` |
| `VSSetup.obj/Release/…/*.vsman.merge`, `*.vsman.overlay` | intermediates of the above |
| `obj/MSBuild.VSSetup{,.Arm64}/…/Microsoft.Build*.{vsix,json}` | intermediate copies |

That is 16 of the 137 expected differences, all from one random 8-byte id.

**Impact:** none on `-mt` (it reproduces identically in the control), but it does mean MSBuild's VS
insertion payload is not bit-reproducible, so two rebuilds of the same commit cannot be shown to
produce the same VSIX by hashing. Worth fixing separately if reproducible VS insertion is a goal.

The harness handles this without going blind: the `.vsix` rule normalizes only the `Id="R…"`
attribute inside `_rels/*` and still compares every other byte of those files and every other entry.
A deliberate one-byte change to `manifest.json` inside a `.vsix` was still reported as
`payloadIdentical=False, contentDiff entries: manifest.json`.

### 3. `.nupkg` entry timestamps

All 27 packages differ. Every packed entry is byte-identical; only the zip's per-entry
`LastWriteTime` differs, because NuGet stamps entries with the wall-clock last-write time of the file
it packed. Compared with the `ComparePayload` disposition, which enforces payload equality and
tolerates the timestamps.

### 4. MSBuild incremental-state caches

70 `*.AssemblyReference.cache` / `*.GenerateResource.cache` files under `obj` differ: they record the
last-write timestamps of their inputs, which are wall-clock. Never shipped.

### 5. `VS with MSBuild.slnx.lnk`

A Windows shortcut generated for local development; embeds link-tracking data. Not a build output.

### 6. Log-level noise that is not `-mt` specific

Visible in the control run, so normalized away for both comparisons:

* `Creating directory "..."` — parallel `Copy`/`MakeDir` invocations race; the loser finds the
  directory already present and logs nothing, so both presence and count vary between identical
  builds.
* Project-started / `Done Building Project` lines — a shared dependency is really built by whichever
  requester reaches it first; every other requester is served from the results cache. Both the
  requester and the exact target subset that executes are scheduling artifacts.
* Target header lines — MSBuild's text logger re-emits a target header whenever output from a
  different node interleaves.
* Roslyn compiler-server startup (`Attempting to create process 'VBCSCompiler.exe'`,
  `Successfully created process with process id N`, `Setting DOTNET_ROOT to ...`) — whether
  `VBCSCompiler` has to be started depends on whether a server from an earlier build is still alive.
* NuGet restore chatter — depends on package-cache warmth.

## `-mt`-specific finding: spurious `TaskAssemblyLocationMismatch` messages

**This is the only behavioural difference `-mt` introduced, and it is log noise only.**

On the official MicroBuild pool (build 14817031) the `-mt` build emits **1 364** normal-importance
messages that the baseline does not, over 9 distinct
(loaded-from, desired-location) pairs:

```
Task assembly was loaded from 'C:\...\MSBuild\Current\Bin\Microsoft.Build.dll'
  while the desired location was '...\Roslyn\binfx\Microsoft.Build.Tasks.CodeAnalysis.Sdk.dll'.
```

Distribution over the "desired" assembly:

| count | desired assembly |
|---|---|
| 556 | `.dotnet\sdk\<version>\Roslyn\binfx\Microsoft.Build.Tasks.CodeAnalysis.Sdk.dll` |
| 274 | `MSBuild\Current\Bin\Microsoft.Build.Tasks.Core.dll` |
| 190 | `MSBuild\Current\Bin\amd64\Microsoft.Build.Tasks.Core.dll` |
| 185 | `Sdks\Microsoft.Build.Tasks.Git\tools\netframework\Microsoft.Build.Tasks.Git.dll` |
| 86 | `Sdks\Microsoft.SourceLink.Common\tools\netframework\Microsoft.SourceLink.Common.dll` |
| 23 | `Sdks\Microsoft.NET.Sdk\tools\net472\Microsoft.NET.Build.Tasks.dll` |
| 22 | `microsoft.testing.platform.msbuild\...\Microsoft.Testing.Platform.MSBuild.dll` |
| 22 | `xunit.v3.core.mtp-v2\...\xunit.v3.msbuildtasks.dll` |
| 6 | `microsoft.visualstudioeng.microbuild.plugins.swixbuild\...\SwixBuild.dll` |

As a side effect, **12** targets that are otherwise silent at normal verbosity acquire a target header
in the `-mt` log only (`AcquireSdk`, `ResolveKeySource`, `InitializeSourceRootMappedPaths`,
`NormalizeNetCoreSdkRootCasing`, `GenerateMSBuildEditorConfigFileCore`, …). That is purely the text
logger reacting to the extra messages: every one of those header blocks contains nothing but this
message. It is not evidence of extra work — the structural comparison shows every target executing
exactly as many times in both runs.

### Root cause

Under `-mt`, tasks that are not marked `[MSBuildMultiThreadableTask]` are routed to an external
TaskHost — confirmed by `Launching task "{0}" from assembly "{1}" in an external task host …` in the
`-mt` binary log. `TaskExecutionHost.InitializeTask` then does:

```csharp
// src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs
string realTaskAssemblyLocation = TaskInstance.GetType().Assembly.Location;
if (!string.IsNullOrWhiteSpace(realTaskAssemblyLocation) &&
    realTaskAssemblyLocation != _taskFactoryWrapper.TaskFactoryLoadedType.Path)
{
    if (!IsTaskAssemblyMatchFactoryType())
    {
        _taskLoggingContext.LogComment(MessageImportance.Normal, "TaskAssemblyLocationMismatch",
            realTaskAssemblyLocation, _taskFactoryWrapper.TaskFactoryLoadedType.Path);
    }
}

bool IsTaskAssemblyMatchFactoryType() => TaskInstance is not TaskHostTask tht
    || tht.LoadedTaskAssemblyInfo.AssemblyLocation == _taskFactoryWrapper.TaskFactoryLoadedType.Path;
```

For a TaskHost-routed task, `TaskInstance` is a `TaskHostTask`, which lives in `Microsoft.Build.dll` —
hence the "loaded from Microsoft.Build.dll" text for every affected task. `IsTaskAssemblyMatchFactoryType`
exists precisely to suppress the diagnostic in that case, but it compares
`TaskHostTask.LoadedTaskAssemblyInfo.AssemblyLocation` (which comes from the task's
`AssemblyLoadInfo`, and is not populated for tasks registered by assembly *name* rather than by path,
as almost all tasks in `Microsoft.Common.tasks` are) with the resolved
`TaskFactoryLoadedType.Path`. The comparison fails and the message is logged.

Because virtually nothing runs in a TaskHost in a normal build, the guard has effectively never been
exercised; `-mt` makes it the common path.

### Impact

* No build output changes — proven byte for byte by the artifact comparison in this same run.
* Noise: ~1 200 extra messages at `MessageImportance.Normal`, i.e. visible at `-v:n` and above and in
  every binary log, in a build that otherwise logs zero warnings. It makes real
  assembly-identity problems undiscoverable, and inflates binlog size.

### Suggested fix

Either populate `AssemblyLoadInfo.AssemblyLocation` for TaskHost-routed tasks, or make the guard
skip the diagnostic whenever `TaskInstance is TaskHostTask` and the resolved task type came from the
same registration (comparing the task *type*, not the wrapper's assembly). A regression test should
assert that a `-mt` build of a project using tasks registered by assembly name logs no
`TaskAssemblyLocationMismatch`.

### Status in the harness

Tracked as a `knownMtOnly` entry in
[`LogNormalizationRules.json`](../../scripts/mt-equivalence/LogNormalizationRules.json): the messages
are reported in their own "Known `-mt`-only log differences" section of every report instead of
failing the comparison, and the rule is applied only to the `-mt` comparison, never to the control.
**Delete that entry once the bug is fixed** so the check becomes strict again.

Measured on the official MicroBuild pool (build 14817031), the `-mt` build emits **1 364** more of
these lines than the baseline, over 9 distinct (loaded-from, desired-location) pairs. The baseline
count is non-zero because a few tasks already need an out-of-process task host for runtime or bitness
reasons; `-mt` makes that the common path.

## Non-MSBuild noise from 1ES SDL injection

The 1ES Official template injects Guardian's Roslyn analyzers into the build. That tooling is not part
of MSBuild and is not deterministic between two runs, so the harness excludes its own outputs:

* `*.gdn.sarif` — Guardian analysis results written into the output tree. Ignored: they are analysis
  results, not build outputs, and on the pool they were emitted in one build of an identical pair and
  not the other.
* `<project>_<guid>_GdnDotnetAnalyzersMerged.ruleset` — a ruleset generated per project whose file
  name embeds a fresh GUID on every build. The GUID is normalized away in logs and the file ignored.

Both are excluded for the `-mt` comparison and the control alike, so they cannot mask a real
difference on one side only.

## Audit of the exclusion rules

Every exclusion was re-examined adversarially against the artifacts and binlogs of green pool build
14817031, on the assumption that a rule might be hiding a real `-mt` difference. Three things came
out of it.

**Every excused artifact difference is demonstrably pre-existing.** Of the 150 paths excused in the
`-mt` comparison, all 150 also differ between the two identical non-`-mt` builds. Not one difference
is excused on the strength of a written rule alone; each has an experimental control.

**The two target-header suppressions were justified by a control that cannot fail.** The `setOnly`
rule for target headers and its `knownMtOnly` counterpart stop comparing how many times a target
header appears — and a target header is exactly what would reveal a target running only under `-mt`.
Their stated justification was the `-DeepCompare` coverage comparison. That comparison does run, but
target coverage was deliberately excluded from the set of extractors allowed to fail it, precisely
because the header-based extraction it uses is itself unstable. So the suppression was justified by a
check that reports and never fails.

The suppression turned out to be *correct* — reading the raw event stream out of all three binlogs
shows the builds are structurally identical: 384 distinct targets over 18 993 executions, 9 306
distinct `(project, target)` pairs, 122 tasks over 16 083 invocations, 53 projects over 1 842 builds,
and zero warnings and errors — every count equal across baseline, `-mt` and control. The extra target
headers in the `-mt` text log are a logging side-effect of the `TaskAssemblyLocationMismatch` bug
above: the file logger only emits a target header when the target logs something, so targets that are
silent at normal verbosity acquire a header once the bug starts logging inside them.

Fifteen target names have a higher header count under `-mt`. Twelve of them appear in the `-mt` log
only, and every one of those header blocks contains nothing but the bug's message. The other three
(`PrepareForBuild`, `_GenerateSourceLinkFile`, `Build`) already appear in the baseline; their extra
headers are the file logger re-emitting a header when output from another node interleaves, which is
what the `setOnly` rule exists for. That re-emission is not `-mt` specific and swings in both
directions between two identical builds — `_CopyFilesMarkedCopyLocal`, for instance, produces 299
headers in the baseline and 440 in the control.

Being right by luck is not the same as being checked, so the structural comparison is now a
first-class tier of `Compare-Binlogs.ps1`: always on, enforced, netted against the control, and
costing about 2.5 seconds per binlog. It is strictly stronger than what it replaces, because it is
taken from the events rather than from rendered text — a target that executes but logs nothing is
invisible to a text comparison at any verbosity, and is caught here. That case is covered by a test.

**Ignored paths left no trace in the report.** `Ignore` rules removed paths from the comparison
without recording anything, so nobody could review what was being hidden without re-running the
build. The artifact report now lists every `Ignore` rule with the number of paths it matched, how
many of those actually differed, and a sample, and flags rules that matched nothing.

One latent defect was found and fixed along the way: `drop` patterns were matched against the raw log
line including the node-id/timestamp prefix, so every `^`-anchored rule silently stopped matching
whenever the replay engine emitted one. The pool's replay does not, which is why this never surfaced,
but `-msbuildEngine dotnet` does. The prefix is now stripped in its own stage ahead of `drop`.

## What this does and does not prove

Proven for the configuration tested (official-style `Release` build of this repo, Windows,
`-msbuildEngine vs` — the official engine — as well as `-msbuildEngine dotnet`):

* the set of produced files is identical;
* every produced file is byte-identical except for outputs that are already non-deterministic without
  `-mt`;
* the VS insertion outputs (`artifacts/VSSetup`: `.vsix`, `.vsman`, `.vsmand`, the component manifests
  and the `VS.ExternalAPIs.MSBuild` package) are included, and every `.vsix` payload is identical
  entry by entry;
* the `-publish` outputs are included: the Build Asset Registry manifest is byte-identical, all 34
  PDBs staged for the symbol server are byte-identical, and all 10 generated symbol packages are
  payload-identical;
* no errors or warnings appear or disappear;
* the same tasks run, bound to the same task assemblies.

Not covered:

* Signing. It needs the MicroBuild plugin, and an Authenticode signature embeds a trusted-timestamp
  countersignature, so two signed runs could never be byte-identical; `-sign` is therefore not passed
  and the comparison is of unsigned binaries. Since signing is a post-processing step applied
  identically to both runs, identical unsigned inputs imply equivalent signed outputs.
* OptProf / NGEN optimization data, for the same reason the official pipeline skips it when OptProf is
  off: both runs would consume the same IBC drop, so it adds cost without signal.
* Feed publishing and the Build Asset Registry push, which happen in the post-build stage rather than
  in the build. The manifest that drives them *is* compared.
* Non-Windows builds and the source-build/VMR leg (covered separately by
  `azure-pipelines/vmr-sb-validation.yml`).
* Test execution — this pipeline deliberately does not run tests; `-mt` test coverage lives in
  `.vsts-dotnet-ci.yml`.
