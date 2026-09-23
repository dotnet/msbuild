# OrchardCore clean-build performance snapshot

<!-- markdownlint-disable MD013 -->

This document records a clean OrchardCore build performance snapshot captured on
September 21, 2026. It focuses on formatting and resource helpers, string builders,
binary logging, overall CPU and allocation hotspots, and total build memory.

## Summary

The formatting helpers are not major CPU or memory hotspots in this workload:

- Visible `MessageFormatter` frames account for 14 ms of sampled CPU (0.01% of
  root CPU) and 1.72 MiB of sampled allocation volume (0.028%).
- The complete `ResourceUtilities` class accounts for 99 ms inclusive and 4 ms
  exclusive sampled CPU, plus 26.80 MiB inclusive and 0 bytes direct allocation.
- `System.Text.StringBuilder` methods account for 162 ms of sampled CPU (0.11%)
  and 156.98 MiB of allocation volume (2.59%).
- Runtime `System.Text.ValueStringBuilder` methods account for 85 ms of sampled
  CPU (0.06%) and 46.57 MiB of allocation volume (0.77%).
- MSBuild's custom `ValueStringBuilder` has no directly attributed CPU or
  allocation sample. Its small methods inline, so this is not proof of literal
  zero cost, but there is no evidence that it is a hotspot.
- A broad cache/writer matrix identified an 8,192-entry direct cache plus weak
  long-string caching as the speed/memory knee. Combined with allocation-free
  writer redirection and unboxed metadata enumeration, the final tracked package
  reduced replay wall time 32.2% and allocation volume 13.1% while producing
  byte-identical output.
- GC pause time is 5.77 seconds, or 0.39% of traced wall time.

The largest resolved managed CPU hotspot is binary-log string hashing:
`BuildEventArgsWriter.HashAllStrings` accounts for 3,252 ms of sampled CPU.

## Workload

- OrchardCore commit: `ca940915513382013d941ddeb86267cfcfe4df75`
- MSBuild commit: `2fd52dc6a0`
- Engine: local Release `net11.0` build with matching portable PDBs
- Target: `OrchardCore.slnx`
- Configuration: default `Debug`
- Logging: normal console verbosity plus binary logger
- Restore: completed before measurement
- Clean: completed immediately before each measured build
- Engine process: direct `dotnet MSBuild.dll`, without `dotnet msbuild` or
  automatic response files
- Multiprocess controls: no `/m`, `/noAutoResponse`, and
  `BuildInParallel=false`
- Server controls: `/nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
  `MSBUILDDISABLENODEREUSE=1`, `UseSharedCompilation=false`, and
  `UseRazorBuildServer=false`

The baseline and traced builds both exited successfully. Each had one root
MSBuild process and no MSBuild, VBCSCompiler, or Razor server descendant. At
most three processes were active simultaneously: the root, one one-shot C#
compiler, and its console host.

The measured build command had this shape:

```text
dotnet MSBuild.dll OrchardCore.slnx -noAutoResponse -t:Build /nodeReuse:false \
  -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -p:UseRazorBuildServer=false -v:normal -nologo -binaryLogger:<path>
```

The traced run nested that command under `dotnet-trace collect --profile
gc-verbose` and `filtrace collect --profile cpu --cpu-ms 1`. A startup hook
cleared diagnostic-port suspension variables after the root runtime resumed so
they did not propagate to child tools.

## Total time and memory

An external process monitor sampled memory every 100 ms. "Root" is the MSBuild
process. "Tree" includes one-shot tools launched by the build. CPU is summed
process CPU; wall time is elapsed time.

| Metric | Baseline | Traced | Trace delta |
| --- | ---: | ---: | ---: |
| Wall time | 1,278.34 s | 1,489.64 s | +16.53% |
| Root CPU | 132.06 s | 141.83 s | +7.39% |
| Process-tree CPU | 2,859.59 s | 3,183.69 s | +11.33% |
| Root peak working set | 1,115.83 MiB | 1,144.71 MiB | +2.59% |
| Root peak private bytes | 1,028.20 MiB | 1,055.13 MiB | +2.62% |
| Process-tree peak working set | 1,868.89 MiB | 1,878.65 MiB | +0.52% |
| Process-tree peak private bytes | 1,743.70 MiB | 1,747.14 MiB | +0.20% |
| Peak simultaneous processes | 3 | 3 | 0 |

ETW records 144.91 seconds of sampled root CPU, 2.17% above the process
counter's 141.83 seconds. The baseline is the better estimate of normal
end-to-end cost; percentages below use the traced root process.

## Overall hotspots

These are exclusive/self rankings. Inclusive rankings are dominated by async
and build-orchestration ancestors, which repeat descendant cost at every stack
level.

### Resolved CPU self time

| # | Frame | CPU | Root CPU |
| ---: | --- | ---: | ---: |
| 1 | `BuildEventArgsWriter.HashAllStrings` | 3,252 ms | 2.24% |
| 2 | `BuildEventArgsWriter.Write` | 1,158 ms | 0.80% |
| 3 | `FowlerNollVo1aHash.ComputeHash64Fast` | 642 ms | 0.44% |
| 4 | `PackedSpanHelpers.IndexOf` | 521 ms | 0.36% |
| 5 | `CastHelpers.IsInstanceOfClass` | 499 ms | 0.34% |
| 6 | `MSBuildNameIgnoreCaseComparer.GetHashCode` | 479 ms | 0.33% |
| 7 | `OrdinalIgnoreCaseComparer.GetHashCode` | 314 ms | 0.22% |
| 8 | `RetrievableEntryHashSet.GetCore` | 283 ms | 0.20% |
| 9 | `Monitor.Enter` | 282 ms | 0.19% |
| 10 | `HashTableUtility.Compare` | 257 ms | 0.18% |
| 11 | `CommunicationsUtilities.GetEnvironmentVariablesWindows` | 256 ms | 0.18% |
| 12 | `ExpressionShredder.TryParseItemVectorExpression` | 242 ms | 0.17% |
| 13 | `BuildEventArgsWriter.HashString` | 228 ms | 0.16% |
| 14 | `MSBuildNameIgnoreCaseComparer.Equals` | 215 ms | 0.15% |
| 15 | `String.GetNonRandomizedHashCodeOrdinalIgnoreCase` | 206 ms | 0.14% |
| 16 | `PropertyExpander.FindClosingParenthesis` | 189 ms | 0.13% |
| 17 | `MetadataExpander.Expand` | 188 ms | 0.13% |
| 18 | `Monitor.Exit` | 182 ms | 0.13% |
| 19 | `TaskItem.SetMetadataOnTaskOutput` | 176 ms | 0.12% |
| 20 | `BuildEventArgsWriter.WriteArguments` | 165 ms | 0.11% |

The resolved top 20 sum to 9,734 ms, or 6.72% of root CPU. Another
115,709 ms (79.85%) is unresolved, predominantly native or system work. This is
therefore a ranking of resolved managed frames, not all CPU.

### Allocation self volume

| # | Allocation leaf | MiB | Root allocations |
| ---: | --- | ---: | ---: |
| 1 | `String.Ctor` | 447.23 | 7.37% |
| 2 | immutable dictionary `SortedInt32KeyNode.SetOrAdd` | 276.30 | 4.55% |
| 3 | `BatchingEngine.BucketConsumedItems` | 181.24 | 2.99% |
| 4 | `BatchingEngine.GetItemMetadataValues` | 176.68 | 2.91% |
| 5 | `Dictionary.Resize` | 129.45 | 2.13% |
| 6 | `BuildEventArgsWriter.RedirectWritesToDifferentWriter` | 125.66 | 2.07% |
| 7 | `List.AddWithResize` | 113.67 | 1.87% |
| 8 | `TaskItemFactory.CreateItem` | 101.97 | 1.68% |
| 9 | `BatchingEngine.PrepareBatchingBuckets` | 100.62 | 1.66% |
| 10 | `StringBuilder.ToString` | 100.57 | 1.66% |
| 11 | `ConditionalWeakTable.Container.Resize` | 98.18 | 1.62% |
| 12 | `ExpressionShredder.TryParseItemVectorExpression` | 92.57 | 1.53% |
| 13 | `TaskExecutionHost.GatherTaskItemOutputs` | 83.29 | 1.37% |
| 14 | `ProjectItemInstanceFactory.CreateItem` | 82.06 | 1.35% |
| 15 | `Lookup.ModifyItems` | 70.02 | 1.15% |
| 16 | `String.Concat` | 66.16 | 1.09% |
| 17 | `List.set_Capacity` | 63.40 | 1.04% |
| 18 | `LazyItemEvaluator.BuildIncludeOperation` | 63.38 | 1.04% |
| 19 | `JsonReaderHelper.TranscodeHelper` | 60.44 | 1.00% |
| 20 | `ConditionEvaluator.EvaluateConditionCollectingConditionedProperties` | 58.90 | 0.97% |

The top 20 account for 2,612,840,552 bytes (2.43 GiB), or 41.07% of
the 5.926 GiB sampled allocation volume.

## Binary-log string hashing investigation

The CPU trace attributes 3,831 ms inclusive and 3,252 ms exclusive to
`BuildEventArgsWriter.HashAllStrings`. Its `HashString` calls and the separate
FNV content-hash frame add more cost. Instrumented replay of the captured
OrchardCore binlog found this workload shape:

| Metric | Original writer | Selected cache design |
| --- | ---: | ---: |
| `HashString` calls | 73,475,871 | 73,475,871 |
| Content-hash computations | 18,565,980 | 1,092,653 |
| UTF-16 code units scanned | 2,393,878,318 | 96,773,268 |
| Median structured replay | 4,344 ms | 2,799 ms |

The original 256-entry direct-mapped reference cache has frequent conflicts.
It also deliberately bypasses strings longer than 4,096 UTF-16 code units. In
this corpus, only 227 distinct string objects in the 16,385-65,536-code-unit
bucket were observed, but those objects were reused 25,415 times. Every reuse
rescanned the complete string.

The selected design has two parts:

1. Increase the direct-mapped cache from 256 to 8,192 entries. This is about
  192 KiB on x64, versus about 6 KiB for the original array, and admit only
  strings of at most 1,024 UTF-16 code units. The admission limit caps retained
  character payload at 16 MiB plus string-object overhead.
2. Cache longer strings in a `ConditionalWeakTable`. This avoids keeping those
  strings alive while allowing repeated references to bypass the content scan.

At the end of the measured replay, all 8,192 direct-cache slots were occupied
and held about 3.31 million UTF-16 code units, or 6.63 MiB of character payload.
The longest retained entry was 1,024 code units. Only 255 distinct string
references in the complete corpus exceeded the 1,024-code-unit admission limit.

Exploratory five-run timings per variant found the following progression. These
runs were not interleaved and overstate the final same-commit A/B gain, but they
identify the cache-size knee: 32,768 entries performed slightly worse despite
fewer content scans.

| Cache variant | Median replay | Change from original |
| --- | ---: | ---: |
| 256 direct entries | 9,757 ms | baseline |
| 1,024 direct entries | 9,596 ms | -1.7% |
| 4,096 direct entries | 8,776 ms | -10.1% |
| 256 direct entries plus weak long strings | about 7,500 ms | -23.1% |
| 4,096 direct entries plus weak long strings | 6,227 ms | -36.2% |
| 16,384 direct entries plus weak long strings | 5,742 ms | -41.2% |
| 16,384 entries, 1,024-unit bound, weak longer strings | 5,635 ms | -42.2% |

### Hash algorithm options

The current hash is a 64-bit FNV-1a-inspired loop over UTF-16 characters. Each
iteration depends on the preceding hash value, so preserving its exact output
does not admit straightforward SIMD parallelism. Four-way scalar unrolling did
not materially improve replay time.

Changing the algorithm was also tested. The runtime implementation of xxHash3
uses hardware-accelerated `Vector256` or `Vector128` paths for long inputs;
xxHash64 processes scalar 32-byte stripes. Before fixing reference reuse,
xxHash3 appeared substantially faster because it accelerated billions of
unnecessary repeated character scans:

| Hash algorithm, original cache | Median replay | Change from FNV |
| --- | ---: | ---: |
| Current FNV | 9,757.44 ms | baseline |
| Four-way unrolled FNV | 9,728.86 ms | -0.3% |
| xxHash3 | 6,461.46 ms | -33.8% |
| xxHash64 | 6,983.71 ms | -28.4% |

After adding a 4,096-entry direct cache and weak long-string cache, the
algorithm result disappeared within run-to-run variance:

| Hash algorithm, improved cache | Median replay | Change from FNV |
| --- | ---: | ---: |
| Current FNV | 6,164.41 ms | baseline |
| xxHash3 | 6,102.92 ms | -1.0% |
| xxHash64 | 6,173.56 ms | +0.1% |

The recommendation is therefore to retain FNV and fix reference reuse. This
avoids a new hashing dependency and preserves existing hash behavior while
capturing nearly all measured benefit.

Hash values are internal and are not serialized, so changing the algorithm
would not itself require a binlog format version. Record identifiers are
serialized, however, and the current dictionaries treat the 64-bit hash as
equality: a collision can silently alias different strings or name/value lists.
That pre-existing correctness risk is independent of this optimization and
should be addressed separately with collision-aware equality rather than used
as a reason to select one 64-bit non-cryptographic hash over another.

The final candidate rewrote all 19,016,311 bytes of the deterministic replay
identically. The replay used structured event deserialization, omitted initial
machine-specific information and project imports, and forced event delivery
with a subscriber. This is a serialization-focused benchmark, not a claim that
the full OrchardCore build becomes 34.5% faster.

### Current prototype impact

The final tracked package was compared with pristine current `HEAD`
(`2fd52dc6a09e5c2173980d89a3584ebae5f23b0e`) in seven interleaved structured
replay pairs. It combines an 8,192-entry direct cache, weak caching above a
1,024-code-unit cutoff, allocation-free writer redirection, an unboxed
`ArrayDictionary` path, empty metadata handling, and helper inlining. The
package won all seven comparisons.

| Metric | Pristine | Final package | Change |
| --- | ---: | ---: | ---: |
| Wall mean | 4,387.35 ms | 2,974.19 ms | -1,413.16 ms / -32.21% |
| Wall median | 4,395.80 ms | 2,930.76 ms | -1,465.04 ms / -33.33% |
| CPU mean | 4,627.23 ms | 3,236.61 ms | -1,390.62 ms / -30.05% |
| Allocation mean | 1,578.13 MiB | 1,371.17 MiB | -206.96 MiB / -13.11% |
| Gen 0 collections | 88.86 | 77.00 | -11.86 |
| Post-GC managed heap | baseline | baseline + 1.90 MiB | +1.90 MiB |

Both variants emitted the same 19,016,311 bytes with SHA-256
`C8EF53361D1ACD7561DF1C7CB1E03D5C78FDA7EC1745749C04092CD1A0558578`.

The cache-size curve shows 8,192 entries are the speed/memory knee:

| Direct entries | Wall gain | Allocations saved | Post-GC heap delta |
| ---: | ---: | ---: | ---: |
| 256 | 23.45% | 207.19 MiB | +0.01 MiB |
| 1,024 | 28.47% | 207.18 MiB | +0.18 MiB |
| 4,096 | 31.72% | 207.14 MiB | +0.82 MiB |
| **8,192** | **34.66%** | **207.07 MiB** | **+1.84 MiB** |
| 16,384 | 35.67% | 206.94 MiB | +4.10 MiB |

The 8K-to-16K step buys about one percentage point more replay speed for an
additional 2.26 MiB retained heap. Writer redirection and concrete
`ArrayDictionary` enumeration account for 135.99 MiB and 73.18 MiB of avoided
allocation respectively. Set associativity, weak fallback for evicted short
strings, record-ID aggregate hashing, 32K caches, and unconditional dictionary
preallocation were rejected.

Four full-build rounds per mode were also collected, with no-binlog controls:

| Mode | Variant | No-binlog mean | Binlog mean | Mean overhead |
| --- | --- | ---: | ---: | ---: |
| Normal | Pristine | 116.933 s | 120.262 s | 3.329 s |
| Normal | Prototype | 116.110 s | 119.009 s | 2.899 s |
| `-mt` | Pristine | 105.624 s | 108.861 s | 3.237 s |
| `-mt` | Prototype | 105.998 s | 108.995 s | 2.996 s |

Normal `/bl` builds averaged 1.253 seconds (1.04%) faster, but the no-binlog
control was also 0.823 seconds faster, leaving a control-normalized estimate of
0.430 seconds. The `-mt` direct result was flat; its control-normalized estimate
was 0.241 seconds faster. Paired standard deviations were 7.152 and 4.034
seconds respectively, so neither whole-build estimate is statistically
reliable. The stable conclusion is localized: serializer wall and CPU time drop
substantially, while the expected whole-build saving is around the one-second
scale and is submerged by local build variance.

### PR 14929 before-and-after reproduction

The binary-logger optimization in PR 14929 was also measured at the exact refs
retained by its performance branches:

- Baseline: `d31c53317cbdc62fc4d8f94dcf8cede87854b345`
- Optimized: `c9906edf6c0dd0f5a58a31f4bd604605755b2661`

Four interleaved clean-build rounds were run per mode. Each measured `Build`
was preceded by an unmeasured `Clean`; compiler and node reuse were disabled.

| Mode | Statistic | Baseline `/bl` | Optimized `/bl` | Local gain | PR gain |
| --- | --- | ---: | ---: | ---: | ---: |
| Normal | Mean | 121.84 s | 120.57 s | 1.27 s / 1.04% | 9.54 s / 7.17% |
| Normal | Median | 121.90 s | 120.14 s | 1.76 s / 1.44% | 9.54 s / 7.17% |
| `-mt` | Mean | 109.63 s | 107.38 s | 2.25 s / 2.05% | 9.02 s / 9.54% |
| `-mt` | Median | 110.41 s | 108.30 s | 2.11 s / 1.91% | 9.02 s / 9.54% |

The local build reproduces the direction but only about one fifth of the PR's
percentage improvement. Mean no-binlog/binlog pairs estimate that normal-mode
logging overhead fell from 5.59 to 2.07 seconds and `-mt` overhead fell from
1.20 to 0.69 seconds. These differences are smaller than run-to-run variance:
the standard deviation of paired overhead reductions was 6.27 seconds normal
and 10.96 seconds under `-mt`, so the exact full-build attribution is not
statistically strong.

A second measurement isolated serialization from compilation. A binlog written
by the baseline commit was replayed through `BinaryLogReplayEventSource` into a
fresh `BinaryLogger`, using `OmitInitialInfo;ProjectImports=None`. Seven
interleaved rounds produced this result:

| Variant | Mean replay | Median replay | Min | Max |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 4,961.93 ms | 4,946.69 ms | 4,757.59 ms | 5,123.42 ms |
| Optimized | 4,347.60 ms | 4,361.95 ms | 4,289.52 ms | 4,404.89 ms |

Optimized won all seven comparisons. Mean wall time improved 12.38%, compared
with approximately 7% reported by the PR's isolated benchmark. Both variants
emitted byte-identical 18,633,782-byte output. This stable replay result confirms
the serializer improvement even though the local full-build signal is noisy and
smaller than GOLDWIN's.

## Formatting and resource helpers

CPU values are 1 ms periodic samples. Allocation values are EventPipe
allocation-tick estimates, not retained bytes.

| Scope | CPU inclusive | CPU exclusive | Allocation inclusive | Allocation exclusive |
| --- | ---: | ---: | ---: | ---: |
| Visible `MessageFormatter` | 14 ms (0.0097%) | 0 ms sampled | 1.72 MiB (0.0284%) | 0 B |
| Complete `ResourceUtilities` | 99 ms (0.0683%) | 4 ms (0.0028%) | 26.80 MiB (0.4417%) | 0 B |
| All `String.Format` | 140 ms (0.0966%) | 26 ms (0.0179%) | 48.33 MiB (0.7964%) | 0 B |

Every allocation under visible `MessageFormatter` and `ResourceUtilities`
ancestry is a descendant result-string allocation in `System.String.Ctor`.
Per-method retained heap cannot be inferred from these allocation stacks.

The principal `ResourceUtilities` inclusive CPU descendants are resource lookup
(`ResourceManager.GetString`, 21 ms), `String.FormatHelper` (62 ms), and visible
`MessageFormatter` calls (14 ms). The main caller is lazy target-started message
formatting.

Release builds omit the debug-only argument validators in `MessageFormatter`.
Fixed-arity overloads delegate directly to `string.Format`; `params` overloads
first return the input unchanged when there are no arguments. JIT inlining can
remove the small wrapper frame, so `String.Format` and `ResourceUtilities` are
useful overlapping upper bounds. These rows must not be added together.

## StringBuilder

Exact `System.Text.StringBuilder` methods account for 162 ms of sampled CPU,
or 0.11% of root CPU. The hottest members are `AppendWithExpansion` (77 ms),
`ToString` (63 ms), and `Append` (17 ms); these member times overlap.

Leading callers are C# code-generation quoting (37 ms), parallel console
logging (28 ms), C# reference command-line construction (32 ms across two
callers), file normalization (11 ms), and `WriteLinesToFile` (6 ms).

`StringBuilder` ancestry accounts for 164,600,984 sampled bytes (156.98 MiB),
or 2.59% of root allocation volume:

| Direct allocation leaf | MiB | StringBuilder scope |
| --- | ---: | ---: |
| `StringBuilder.ToString` | 100.57 | 64.07% |
| `AppendWithExpansion` | 25.88 | 16.49% |
| uninitialized backing arrays | 21.51 | 13.70% |
| `ExpandByABlock` | 4.65 | 2.96% |
| constructors | 2.64 | 1.68% |

Most allocation is output strings and buffer growth, not builder objects. This
is consistent with MSBuild's thread-static
[`StringBuilderCache`](../src/Framework/Utilities/StringBuilderCache.cs), which
retains one builder per thread when capacity is at most 512 characters.

## ValueStringBuilder

Exact runtime `System.Text.ValueStringBuilder` methods account for 85 ms of
sampled CPU (0.06%). Callers are path normalization (49 ms),
`String.FormatHelper` (23 ms), `AppendSlow` growth (12 ms), and `String.Join`
(1 ms). Its allocation ancestry accounts for 48,830,712 bytes (46.57 MiB), all
sampled allocation being final strings produced by `ToString`.

No CPU sample is directly attributed to
[`Microsoft.Build.Utilities.ValueStringBuilder`](../src/Framework/Utilities/ValueStringBuilder.cs),
and no allocation sample contains it. Its tiny methods inline, so this does not
prove zero cost. Principal call sites start with stack storage; a capacity-based
instance rents immediately, stack-backed growth can rent arrays, and pool rents
can reuse arrays without a new GC allocation. The trace shows no evidence that
the custom builder is a hotspot.

Runtime and custom `ValueStringBuilder` are distinct. Runtime builder allocation
overlaps `String.Format`; the categories must not be added.

## Managed memory and GC

EventPipe records 58,072 allocation samples representing 6,362,479,280 bytes
(5.926 GiB) allocated by the root process, approximately 4.07 MiB/s during the
traced build.

| GC metric | Value |
| --- | ---: |
| Collections | 334 |
| Gen 0 / Gen 1 / Gen 2 | 199 / 127 / 8 |
| Total GC pause | 5,768.38 ms |
| Longest pause | 64.61 ms |
| Mean pause | 17.27 ms |
| Time in GC | 0.39% |
| Peak managed heap | 993.41 MB |
| Cumulative promoted data | 3,819.94 MB |

GC pause time is small relative to wall time. The 1,144.71 MiB traced root peak
working set and 1,055.13 MiB traced root peak private bytes are consistent with
the managed-heap peak plus runtime and native state.

## Conclusions

1. Binary-log string hashing and serialization are the leading resolved managed
  CPU costs. Direct `BuildEventArgsWriter` entries in the top 20 total at least
  4,803 ms, excluding hashing helpers. Reference caching is more effective than
  replacing the hash algorithm for the measured workload.
2. Formatting and resource helpers are not significant CPU or allocation
   hotspots in this workload.
3. Do not prioritize replacing MSBuild's custom `ValueStringBuilder`; its
   stack-first and pool-backed design shows no visible heap allocation hotspot.
4. Runtime `ValueStringBuilder` allocation is final-string construction and
   overlaps `String.Format`; it is not avoidable builder-object allocation.
5. `StringBuilder.ToString` is primarily output cost. Avoiding it generally
   requires avoiding or deferring the resulting text, not swapping builders.
6. Investigate `StringBuilder` growth only at proven dominant callers. Roughly
   52 MiB is expansion or backing-array allocation in a 5.926 GiB workload.
7. GC pause is not a primary wall-time bottleneck in this trace.

## Limitations

- CPU values are periodic 1 ms samples, not instrumented method timers.
- The scoped CPU profile resolves 39% of all frames to method names. Unresolved
  frames are predominantly native or system work. Named managed target frames
  are resolved, but the global CPU ranking is conservative.
- `MessageFormatter` (14 samples), `ResourceUtilities` (99), `StringBuilder`
  (162), and runtime `ValueStringBuilder` (85) are below the 200-sample
  directional-confidence guideline. Exact milliseconds are approximate.
- Allocation values are sampled allocation volume, not retained bytes.
- External process memory and process-tree CPU were sampled every 100 ms. Very
  short child processes or transient memory spikes can be missed.
- Inclusive categories overlap and must not be summed.
