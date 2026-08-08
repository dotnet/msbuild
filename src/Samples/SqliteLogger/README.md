# SqliteLogger — MSBuild Logger with SQL-Queryable Output

## Overview

SqliteLogger is an MSBuild `INodeLogger` that writes structured build events to a SQLite database.
Instead of producing a binary log (`.binlog`) that requires specialized tooling to read, it creates a
relational database that can be queried with any SQLite client—`sqlite3`, DBeaver, Python, C#, or
even a spreadsheet app.

```
dotnet build -logger:"SqliteLogger,SqliteLogger.dll;LogFile=build.sqlite"
```

## Capabilities

### What it captures

| Data category | Tables | Opt-in flag |
|---|---|---|
| Build lifecycle | `Build` | always |
| Evaluation timing | `Evaluations` | always |
| Evaluation properties | `EvaluationProperties` | default on (disable: `NoEvalProperties`) |
| Evaluation items + metadata | `EvaluationItems` | default on (disable: `NoEvalItems`) |
| Project execution tree | `Projects` (parent/child, global props, eval FK) | always |
| Target execution | `Targets` (incl. skipped targets with reason) | always |
| Task execution | `Tasks` (with assembly location) | always |
| Task input/output parameters | `TaskParameters`, `TaskParameterItems`, `TaskParameterMetadata`, `Strings` | `IncludeTaskInputs` |
| Errors and warnings | `Diagnostics` (with full location info) | always |
| Build messages | `Messages` (Normal+ importance) | always (Low importance: `VerboseMessages`) |
| Imported/generated files | `Files` (content as BLOB) | always |

### Pre-built views

The schema includes views for common analysis patterns:

| View | Purpose |
|---|---|
| `ExpensiveProjects` | Projects ranked by total duration |
| `ExpensiveTargets` | Targets ranked by total duration, with ran/skipped counts |
| `ExpensiveTasks` | Tasks ranked by total duration, with min/max/avg |
| `Errors` / `Warnings` | Filtered diagnostics |
| `NodeTimeline` | Per-node target execution timeline for parallelism analysis |
| `FilesWithContent` | Quick browse of embedded file sizes |
| `TaskParametersView` | Fully denormalized task parameters with resolved strings and metadata |
| `TaskParameterItemsView` | One row per task parameter item with resolved ItemSpec |
| `TaskParameterMetadataView` | One row per metadata entry with resolved key/value |
| `ProjectItemTimeline` | Chronological item state changes per project (eval baseline + runtime mutations) |
| `ProjectPropertyTimeline` | Chronological property value changes per project (eval baseline + runtime mutations) |

### Project state reconstruction

The `ProjectItemTimeline` and `ProjectPropertyTimeline` views enable reconstructing the full state
of a project's properties and items at any point during execution:

```sql
-- Get all Compile items for a project, ordered by when they appeared
SELECT Source, ItemSpec, Metadata, TargetName, TaskName
FROM ProjectItemTimeline
WHERE ProjectId = 7 AND ItemType = 'Compile'
ORDER BY TimestampMs;

-- Trace how a property evolved during the build
SELECT Source, PropertyValue, TargetName, TaskName
FROM ProjectPropertyTimeline
WHERE ProjectId = 7 AND PropertyName = 'OutputPath'
ORDER BY TimestampMs;

-- Get the "final" value of a property (last write wins)
SELECT PropertyValue FROM ProjectPropertyTimeline
WHERE ProjectId = 7 AND PropertyName = 'OutputPath'
ORDER BY TimestampMs DESC LIMIT 1;
```

The evaluation rows provide the baseline state; subsequent `AddItem`, `RemoveItem`, and `TaskOutput`
rows show mutations. This solves one of the hardest problems in binlog analysis: knowing the state of
items and properties at a specific point in the build without replaying the entire event stream
programmatically.

### String interning

Task parameter data uses a normalized schema with a `Strings` table for deduplication. Item specs
and metadata keys/values are stored as integer foreign keys, significantly reducing storage for
builds with many repeated paths and metadata values. Well-known file-system metadata
(`FullPath`, `RootDir`, `Filename`, `Extension`, etc.) is stripped since it can be recomputed from
`ItemSpec`.

### Embedded source files

Like the binary logger, SqliteLogger embeds the content of imported `.props`/`.targets` files,
generated files, metaprojects, and response files into the `Files` table. This means the database
is self-contained—you can examine the exact MSBuild files that were used during the build without
needing access to the original machine.

## Parameters

```
-logger:"SqliteLogger,SqliteLogger.dll;LogFile=build.sqlite;IncludeTaskInputs;VerboseMessages"
```

| Parameter | Effect |
|---|---|
| `LogFile=path.sqlite` | Output file path (default: `msbuild.sqlite`) |
| `IncludeTaskInputs` | Record task input/output parameters (adds significant data) |
| `VerboseMessages` | Include Low importance messages |
| `NoEvalProperties` | Skip evaluation properties |
| `NoEvalItems` | Skip evaluation items |

## Tradeoffs vs. Binary Logs

### Advantages of SqliteLogger

| Aspect | SqliteLogger | Binary Log |
|---|---|---|
| **Queryability** | Standard SQL — any SQLite client works | Requires `MSBuildStructuredLog`, `BinlogTool`, or `Microsoft.Build.Logging` APIs |
| **Ad-hoc analysis** | Write a SQL query, get instant results | Write C# code to walk the event tree |
| **Cross-tool access** | Python, R, Excel, DBeaver, CLI `sqlite3` | .NET-only (or replay to text) |
| **Aggregation** | `GROUP BY`, `SUM`, `AVG` are native | Must accumulate manually in code |
| **State reconstruction** | Timeline views provide chronological property/item changes | Must replay events in order, tracking state manually |
| **Relational joins** | Join projects→targets→tasks→parameters in one query | Navigate parent/child references in code |
| **Partial reads** | Read only the tables/rows you need | Must decompress and walk the full stream |
| **Multi-build comparison** | Load multiple databases, `ATTACH`, and compare | No built-in mechanism |
| **Schema documentation** | Tables and columns are self-describing | Event args classes require API documentation |

### Advantages of Binary Logs

| Aspect | Binary Log | SqliteLogger |
|---|---|---|
| **File size** | ~400 KB for a simple build (internally GZip-compressed) | ~1.8–3.3 MB for the same build (see analysis below) |
| **Compressed size** | Already compressed; gzip adds <1% | Compresses to ~250–650 KB (still larger) |
| **Replay fidelity** | Lossless — exact event stream can be replayed | Structured extraction; some event-level detail lost |
| **Build replay** | Can replay a binlog as if re-running the build | Not possible — data is decomposed into tables |
| **Ecosystem tooling** | MSBuild Structured Log Viewer, `dotnet build --binlog` | No dedicated viewer (but any SQL tool works) |
| **Streaming writes** | Append-only compressed stream, minimal overhead | SQLite transactions with periodic commits |
| **Official support** | First-party, maintained by MSBuild team | Sample/experimental |
| **Round-trip** | Input to `ProjectCollection.BuildProject` replay | One-way extraction |
| **Custom events** | Preserved verbatim | Not captured unless they're a known subtype |

### When to use which

- **Use binlog** when you need lossless build replay, official tooling support, or minimum file size.
- **Use SqliteLogger** when you want to query build data with SQL, compare builds side-by-side,
  reconstruct project state at specific points, or integrate with data analysis tools.
- **Use both** when you want the safety net of a binlog plus the queryability of SQL:
  ```
  dotnet build /bl:build.binlog -logger:"SqliteLogger,SqliteLogger.dll;LogFile=build.sqlite;IncludeTaskInputs"
  ```

## Size Analysis

All measurements from building a `dotnet new console` project (`net10.0`, single `Program.cs`).

### Raw file sizes

| Configuration | SQLite | Binlog | SQLite / Binlog |
|---|---:|---:|---:|
| Default (eval props + items, no task inputs) | 2,486 KB | 401 KB | 6.2x |
| With `IncludeTaskInputs` | 3,301 KB | 394 KB | 8.4x |
| No eval data (`NoEvalProperties;NoEvalItems`) | 1,851 KB | 393 KB | 4.7x |

### Compressed (gzip -9)

| Configuration | SQLite.gz | Binlog.gz | SQLite.gz / Binlog.gz |
|---|---:|---:|---:|
| Default | 371 KB | 410 KB | 0.9x |
| With `IncludeTaskInputs` | 633 KB | 402 KB | 1.6x |
| No eval data | 253 KB | 402 KB | 0.6x |

Binary logs are already internally GZip-compressed, so external compression adds <1%. SQLite
databases compress very well (to 14–20% of original size) because of repeated strings and
sparse pages.

**Key insight**: When compressed, the default-mode SQLite database is actually *smaller* than
the binlog. The binlog is larger in this configuration because it always includes evaluation
properties and items in its compressed stream, while the SQLite database stores them more
efficiently in normalized tables. With `IncludeTaskInputs`, the SQLite database grows by ~800 KB
(compressed) due to the normalized task parameter tables.

### What's in the bytes: size breakdown by category

#### Default mode (eval data, no task inputs) — 2,486 KB

| Category | Data bytes | % of total | Notes |
|---|---:|---:|---|
| Embedded files | 1,459 KB | 58.7% | `.props`, `.targets`, `.csproj` content (BLOB) |
| Evaluation properties | 118 KB | 4.7% | 2,025 properties × (name + value) |
| Evaluation items | 218 KB | 8.8% | 1,958 items × (type + spec + JSON metadata) |
| Messages | 46 KB | 1.9% | 50 Normal+ importance messages |
| Build structure | 65 KB | 2.6% | Projects, Targets, Tasks (names, file paths) |
| SQLite overhead | ~579 KB | 23.3% | Page alignment, indexes, B-tree nodes, free space |

#### With `IncludeTaskInputs` — 3,301 KB

| Category | Data bytes | % of total | Notes |
|---|---:|---:|---|
| Embedded files | 1,459 KB | 44.2% | Same as above |
| Evaluation state | 356 KB | 10.8% | Properties + items (slightly more items captured) |
| Task parameters | 1,018 KB | 30.8% | 514 params, 4,652 items, 34,728 metadata entries |
| ↳ Interned strings | 111 KB | 3.4% | 1,665 unique strings |
| ↳ Parameter items | 73 KB | 2.2% | 4,652 FK references |
| ↳ Parameter metadata | 814 KB | 24.7% | 34,728 key-value FK triples |
| Messages | 2 KB | 0.1% | 27 messages (fewer with task inputs enabled) |
| Build structure | 65 KB | 2.0% | Same as above |
| SQLite overhead | ~401 KB | 12.1% | Page alignment, indexes, B-tree nodes |

### Size drivers

1. **Embedded files dominate** (59% of default mode). These are the same `.props`/`.targets` files
   the binary logger embeds. For a simple console app, the SDK ships ~116 imported files totaling
   ~1.4 MB. This is a fixed cost regardless of project complexity—larger projects add relatively
   little to this category.

2. **Task parameter metadata is the growth factor** when `IncludeTaskInputs` is enabled. The 34,728
   metadata rows (814 KB) represent the item metadata flowing through every task. This scales
   linearly with project complexity.

3. **Evaluation state is modest** (~350 KB) thanks to the relational schema. Property names and
   values are stored directly; item metadata is stored as compact JSON. For comparison, the same
   data in the binlog's serialized event format is larger but benefits from GZip compression.

4. **SQLite overhead** (page alignment, B-tree structure, indexes) accounts for 12–23% of the file.
   This is the cost of random-access queryability. Running `VACUUM` reclaims unused pages; the
   measurements above are post-vacuum.

5. **String interning** saves significant space for task parameters. Without interning, the 4,652
   item specs and 34,728 metadata values would store full strings inline. With interning, 1,665
   unique strings are stored once, and all references are 8-byte integer FKs.

### Scaling expectations

For larger builds (e.g., a full solution with hundreds of projects):
- **Embedded files** grow sub-linearly (many projects import the same SDK files, stored once via `UNIQUE` constraint)
- **Evaluation data** grows linearly with the number of evaluations × properties/items per project
- **Task parameters** grow linearly with task count × parameters per task (this is the largest factor)
- **Build structure** (projects, targets, tasks) is typically small relative to the data they contain
- **Compression ratio improves** with scale because larger databases have more repeated patterns
