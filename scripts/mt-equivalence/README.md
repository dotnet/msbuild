# `-mt` build equivalence harness

Proves that building MSBuild's official artifact set with MSBuild running in multithreaded mode
(`-mt`) produces the same outputs as building it without `-mt`.

The Azure DevOps pipeline that runs this is
[`azure-pipelines/.vsts-dotnet-mt-equivalence.yml`](../../azure-pipelines/.vsts-dotnet-mt-equivalence.yml).
It runs daily at 02:00 UTC against `main` with `always: true`, so it catches `-mt` regressions
introduced by the whole dependency surface (SDK, arcade, Roslyn, NuGet), not only by commits to this
repo. A red run means `-mt` changed something.

## What it does

`Run-MTEquivalence.ps1` runs the *same* official-style build command up to three times **in the same
working tree** (so every absolute path baked into an output is identical) and then compares the
resulting `artifacts` trees:

| run | command | purpose |
|---|---|---|
| `baseline` | `build.cmd -pack -ci -configuration Release <official properties>` | reference |
| `mt` | the same command with `MSBUILD_MT_ENABLED=1` | the thing under test |
| `control` | the same command again, no `-mt` anywhere | scientific control |

`MSBUILD_MT_ENABLED=1` is the mechanism arcade already provides: `eng/common/tools.ps1` appends `-mt`
to the MSBuild command line when it is set. The build command is otherwise the command the official
build runs, minus four things:

| dropped | why |
|---|---|
| `-sign` | needs the MicroBuild signing plugin, and an Authenticode signature embeds a trusted-timestamp countersignature, so signed binaries could never be byte-identical between two runs. What `-mt` could actually affect is the *content* being signed, and that is compared unsigned |
| `EnableNgenOptimization` | this is exactly the official pipeline's `$(SkipApplyOptimizationDataArg)`, which it passes whenever OptProf is off. Leaving it on requires a `VisualStudioDropAccessToken` and downloads an IBC drop that both runs would consume identically |
| `GenerateSbom` | SBOM manifests embed a generation timestamp and a per-run GUID, so they can never be byte-compared |

`-publish` **is** passed. It is the *official-build* publish, not `dotnet publish`, and with
`DotNetPublishUsingPipelines=true` it pushes nothing to any feed — it emits `##vso[artifact.upload]`
logging commands and produces three real outputs that would otherwise never be built:

| output | compared as |
|---|---|
| `log/<config>/AssetManifest/*.xml` — the Build Asset Registry manifest listing every package, blob and PDB the build declares | byte-identical |
| `tmp/<config>/SymbolPackages/*.symbols.nupkg` — 10 generated symbol packages | payload byte-identical |
| `tmp/<config>/PDBsToPublish/**` — 34 PDBs staged for the symbol server | byte-identical |

Feed publishing happens in the separate post-build stage, which this pipeline does not run. Note that
those three live under `log/` and `tmp/`, which are otherwise ignored, so `ArtifactCompareRules.json`
carves them out *ahead* of the ignore rules.

`VisualStudioDropName` is also passed (mirroring the official job's `VisualStudio.DropName`): without
it `AfterSigning.proj` fails outright. It is only embedded into the generated VS insertion manifests —
nothing is uploaded — and it is identical across the runs being compared.

Everything else — `-pack`, `-ci`, `OfficialBuildId`, `RepositoryName`, `TeamName`,
`DotNetPublishUsingPipelines`, `SuppressFinalPackageVersion`, `IsExperimental` — is passed exactly as
the official job passes it, so the same projects, targets and packaging run.

### Use `vs`, not `dotnet`

The official build runs `msbuildEngine: vs`, and only that engine produces the **VS insertion
outputs**: `artifacts/VSSetup` (`.vsix`, `.vsman`, `.vsmand`), the `VS.ExternalAPIs.MSBuild` package
and the arm64 flavours. On this repo that is 28 011 artifact files versus 23 329 with `dotnet` — so
validating with `dotnet` silently skips the entire insertion surface. `vs` is the default here for
that reason; `-MSBuildEngine dotnet` remains available for machines without a new enough VS.

**The control run is what makes a difference interpretable.** It measures how much this repo's build
output varies between two *identical* builds. Any difference that also shows up in the control is
pre-existing build non-determinism, not something `-mt` caused. Without it, an `mt`-vs-`baseline`
diff cannot be attributed to anything.

### Which MSBuild's `-mt` this validates

`MSBUILD_MT_ENABLED=1` makes arcade pass `-mt` to **the MSBuild that drives the build** — the pool's
Visual Studio MSBuild with `-MSBuildEngine vs`, or the SDK pinned in `global.json` with `dotnet`. So
this pipeline answers *"can the official build be flipped to `-mt` today?"*.

That is deliberately a different question from the one `.vsts-dotnet-ci.yml` answers. CI passes
`-stage2Arguments /mt`, which applies `-mt` to a stage-2 build driven by the **freshly built** MSBuild,
answering *"does the `-mt` implementation in this PR break a build?"*. Both are worth having; do not
read a green run here as coverage for in-development `-mt` changes.

A practical consequence: the MSBuild on the agent must itself support `-mt`, or only the `mt` build
fails (with MSB1001 for an unrecognized switch). The orchestrator prints an explicit hint in that case.

## What it compares

### 1. Artifacts, byte for byte — `Compare-Artifacts.ps1`

Hashes every file under `artifacts` on both sides and classifies each path with an ordered rule set
([`ArtifactCompareRules.json`](ArtifactCompareRules.json)):

| disposition | meaning |
|---|---|
| `Compare` (default) | must be byte-identical; a difference fails the run |
| `ComparePayload` | zip container: the *entries* must be byte-identical, but the zip's per-entry timestamps may differ. A rule may add `normalizeEntries` to strip one documented per-build value out of specific entries (see `.vsix` below) while still comparing everything else in them |
| `Informational` | non-deterministic by construction; reported with a written reason, does not fail |
| `Ignore` | excluded (logs, temp, SBOM manifests, dev-environment helper files) |

Note that patterns are matched with PowerShell `-like`, where `*` also crosses directory separators —
so a pattern must not be prefixed with `**/` if it needs to match a root-relative path such as
`VSSetup/Release/...`.

Two reclassifications are derived rather than hard-coded, so the rule file stays small and honest:

* **MVID-only PE differences.** If the only differing bytes in an assembly are the COFF
  `TimeDateStamp`, the optional-header `CheckSum`, and a single 16-byte window, the assembly was
  compiled with `/deterministic-` and its MVID is a random GUID. Under `/deterministic+` the MVID is
  a hash of the emitted content, so an MVID-only delta cannot be caused by the build engine.
* **Payload-identical zips.** A `.nupkg` whose entries all hash equal but whose entry timestamps
  differ is reported as payload-identical (NuGet stamps entries with the wall-clock last-write time
  of the packed file).

Differing files are triaged automatically: PE images are reported per PE section, zips per entry,
text files by first differing line.

### 2. Logs, functionally — `Compare-Binlogs.ps1`

Binary logs can never be byte-identical (timestamps, durations, node ids, event ordering), so they
are compared functionally:

1. **`-mt` evidence.** The MSBuild command line is read back out of each binlog (it is recorded in
   the first few KB, so this costs one gzip read) and the script asserts that the `mt` run really did
   run with `-mt` and the baseline did not. Without this the whole comparison could silently become
   vacuous.
2. **Diagnostics.** Both logs are replayed at quiet verbosity; the multiset of errors and warnings
   must be identical.
3. **Functional.** Both are replayed at normal verbosity, normalized with
   [`LogNormalizationRules.json`](LogNormalizationRules.json), and compared as line multisets.
4. **Coverage** (`-DeepCompare`). Both are replayed at diagnostic verbosity and the sets of executed
   tasks and of task→assembly bindings must match. The set of *target* names is reported but not
   enforced: the text logger only emits a target header when a target produces output in a contiguous
   block, so the set is partly an interleaving artifact — the control run shows the same instability.

Every normalization rule carries a `reason` explaining why the raw text cannot be compared verbatim.

### 3. Netting the `-mt` differences against the control

When the control run is present, `Run-MTEquivalence.ps1` computes the verdict that actually matters:
**differences attributable to `-mt`**. Any artifact path or log line that differs in the `mt`
comparison *and* also differs in the `control` comparison has just been demonstrated, in that same
run, to differ without `-mt` being involved, so it is reported as explained by the control rather than
counted against `-mt`.

This keeps the pipeline strict about `-mt` while staying robust to pre-existing non-determinism that
the static rule set has not seen yet — for example, outputs that only exist on a signing-enabled
MicroBuild agent. Without a control run (`-SkipControl`) the verdict falls back to the raw
`mt`-vs-`baseline` result, which is stricter but attributes nothing.

The run always fails if the `-mt` evidence check fails, regardless of everything else: a comparison
that cannot prove `-mt` was actually on is worthless.

### Exit codes

| situation | verdict |
|---|---|
| `-mt` could not be proven to have run with `-mt` | **fail** (exit 1) — the comparison proves nothing |
| a difference is attributable to `-mt` | **fail** (exit 1) |
| the control run shows non-determinism the rules do not explain | **warning**, exit unaffected — real, worth fixing, but not an `-mt` regression |
| otherwise | pass (exit 0) |

Control-run problems are deliberately a warning rather than a failure: failing the pipeline for
pre-existing build non-determinism would train people to ignore a red run, which is exactly when the
`-mt` signal matters. In Azure DevOps both are also emitted as `##vso[task.logissue]` so they show up
on the run summary.

### Known `-mt`-only log differences

`LogNormalizationRules.json` has a `knownMtOnly` section for differences that are already filed as
bugs. They are reported in their own section of the report instead of failing the comparison, and are
applied **only** to the `mt` comparison, never to the control. Delete an entry when its bug is fixed.

## Running it locally

```powershell
# Full run: three builds plus all comparisons (about 1.5-2 hours on a dev box).
pwsh scripts/mt-equivalence/Run-MTEquivalence.ps1 -WorkDir D:\mtcmp -DeepLogCompare

# Faster: skip the control run.
pwsh scripts/mt-equivalence/Run-MTEquivalence.ps1 -WorkDir D:\mtcmp -SkipControl

# Iterate on the comparison rules against snapshots you already produced.
pwsh scripts/mt-equivalence/Run-MTEquivalence.ps1 -WorkDir D:\mtcmp -SkipBuilds -DeepLogCompare
```

On a machine without the Visual Studio version pinned in `global.json`, add `-MSBuildEngine dotnet`.

Each snapshot of `artifacts` is about 4.5 GB, and up to three are kept at once, so budget ~15 GB of
free space for `-WorkDir`.

Reports land in `<WorkDir>\reports`:

* `summary.md` – everything in one file (this is what the pipeline uploads as the build summary)
* `artifact-compare.<label>.{json,md}`
* `log-compare.<label>.{json,md}`

## Results

Three official-style `Release` builds per trio, all on `main`, with `-MSBuildEngine vs` — the official
configuration — producing 28 061 artifact files each, including the full VS insertion surface and the
`-publish` outputs.

| comparison | byte-identical | unexpected | expected |
|---|---|---|---|
| `mt` vs `baseline` | 27 909 | **0** | 148 |
| `control` vs `baseline` | 27 911 | **0** | 146 |

**`-mt` introduced no difference in the produced bits at all**, including every VS insertion output,
the Build Asset Registry manifest, all 34 staged PDBs and all 10 symbol-package payloads. Every file
that differs between the `-mt` build and the baseline also differs between two identical non-`-mt`
builds:

| class | count | why |
|---|---|---|
| `*.AssemblyReference.cache`, `*.GenerateResource.cache` | 81 | MSBuild incremental state keyed on wall-clock file timestamps |
| `*.nupkg` (packages + symbol packages) | 38 | zip entry timestamps; payloads byte-identical |
| `.vsman` / `.vsmand` / `.merge` / `.overlay` / component `.json` | 12 | embed the size and SHA256 of a `.vsix` that is not byte-reproducible (see below) |
| 6 test assemblies × (bin + obj) | 12 | compiled with `/deterministic-`, so the MVID is a random GUID |
| `*.vsix` | 4 | random OPC relationship id; payloads verified byte-identical |
| `VS with MSBuild.slnx.lnk` | 1 | Windows shortcut, embeds link-tracking data |

The only `-mt`-specific behaviour observed is log noise, tracked in
[`documentation/specs/mt-build-equivalence-findings.md`](../../documentation/specs/mt-build-equivalence-findings.md),
which also documents the two pre-existing reproducibility gaps found along the way
(`/deterministic-` test assemblies and the non-reproducible `.vsix`).

### The harness can fail

An always-green check is worthless, so every failure path was exercised:

* Removing the `knownMtOnly` allowance surfaced the 21 real `-mt`-only log lines: `logs=FAIL`,
  21 unexplained, exit code 1.
* Flipping a single byte at `0x4000` in `Microsoft.Build.dll` in the `mt` snapshot was reported as
  `PE: 1 bytes, regions: .text@0x4000+1`: `artifacts=FAIL`, exit code 1.
* Appending one byte to `manifest.json` *inside* `Microsoft.Build.vsix` — the one place the
  comparison was deliberately relaxed — was still caught and named:
  `payloadIdentical=False, contentDiff entries: manifest.json`, exit code 1.
* Feeding a non-`-mt` binlog as the `-mt` candidate was rejected by the evidence guard
  (`Candidate build did not run with -mt`), exit code 1.
* Corrupting a byte in the **control** snapshot only produced a warning and exit code 0 — a
  pre-existing non-determinism must not masquerade as an `-mt` regression.

## Files

| file | role |
|---|---|
| `Run-MTEquivalence.ps1` | orchestrator: runs the builds, snapshots them, runs both comparisons, writes `summary.md` |
| `Compare-Artifacts.ps1` | byte-level artifact tree comparison |
| `Compare-Binlogs.ps1` | functional binary-log comparison plus the `-mt` evidence assertion |
| `ArtifactCompareRules.json` | per-path dispositions, each with a reason |
| `LogNormalizationRules.json` | log normalization + known `-mt`-only differences, each with a reason |
| `MtCompareNative.cs` | small C# helper (parallel hashing, byte-run diffing, log scanning) compiled with `Add-Type` |
