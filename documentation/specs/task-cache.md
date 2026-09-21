# Task-cache implementation and format

The [decision record](task-cache-decisions.md) explains the design.
The [user guide](../wiki/Task-Cache.md) describes usage and task-author requirements.

## Execution and ownership

After conditions, batching, and parameter binding, `TaskInvocationCache` keys
the invocation and queries storage. A hit restores files and typed outputs;
normal MSBuild binding applies the current `<Output>` elements.
Target timestamp skipping remains unchanged.

`TaskCacheOwner` holds one BuildXL cache/session from `BeginBuild` to `EndBuild`.
`TaskCacheClient` routes worker requests through existing node communication.
Workers hash inputs and restore files locally; the owner stores files by path.
No artifact bytes are transported through node messages.
Unix output staging files are created owner-only before writing content;
recorded final permissions are applied after validation.

Requests carry an ID, operation, and key. Responses complete waiters directly
on receipt, independently of scheduler work that might be waiting for a task.
Cancellation waits for operation completion; connection loss releases waiters.
Shutdown drains active calls before closing storage. Worker protocol compatibility
is checked by the handshake, independently of the on-disk format.

`BuildManager` resets configuration/results between cache-enabled build sessions
so an old in-memory build result cannot bypass invocation processing.

## Invocation key

The key is SHA-256 over an encoded invocation descriptor and declared inputs:

- Engine/task module identities and task type.
- Project/toolset, runtime/platform, culture, and task execution directory.
- Bound parameter values and effective declared file parameters.
- Referenced output-parameter names, effective environment, and output paths.
- Each declared input's absolute path, existence flag, and content hash.

Names are sorted where ordering is not meaningful; item metadata is canonicalized.
Item inputs retain both the transport representation and task-visible custom
metadata values, so inherited expressions and literal metadata remain distinct.
File bytes are hashed directly, not embedded in the descriptor. Engine module
identity isolates incompatible encodings; there are no explicit format-version
fields or cross-engine migration guarantees.

## Result manifest

`TaskCacheStore` writes one manifest per invocation. BuildXL memoization maps
the invocation key to a content-hash list: manifest first, then present artifacts.
The manifest itself is an opaque CAS object.

Integers use `BinaryWriter`'s little-endian representation. Booleans occupy one
byte. A *manifest string* is an Int32 UTF-8 byte length followed by its bytes.

| Field, in serialization order | Encoding |
| --- | --- |
| Output-path count | Int32 |
| For each output: absolute path | Manifest string |
| Output exists | Boolean |
| Digest, only if present | Manifest string: uppercase hexadecimal SHA-256 |
| Original last-write time | Int64 UTC ticks; zero if absent |
| Unix permissions | Int32, masked to `0x1ff`; zero if absent/not captured |
| Task-state byte count | Int32 |
| Task state | Bytes, described below |
| Checksum | SHA-256 of all preceding manifest bytes |

Output records follow sorted, deduplicated paths and must match the current
contract exactly. An absent record deletes that destination on restore.
Stored timestamps are validated, but restored files receive the current time.
Limits: 64 MiB per manifest and 1 MiB per manifest string.

### Typed outputs

Task state begins with an Int32 output count. Entries are ordinally sorted by
task parameter name. Each contains:

1. Name via `BinaryWriter.Write(string)` (7-bit UTF-8 byte length).
2. Int32 value byte count.
3. Value bytes from `TaskParameter.Translate` through `BinaryTranslator`.

Supported values are nulls, the primitive/array subset accepted by
`TaskInvocationCache.IsSupportedParameterType`, and task items with metadata.
Type tags and restored values are validated; arbitrary task-defined objects
are not deserialized. Each binding receives a fresh value.

### Warnings

Typed outputs are followed by an Int32 warning count and these records:

1. Subcategory, Code, File, Message, HelpKeyword, SenderName, HelpLink:
   Int32 UTF-8 byte length and bytes, with -1 meaning null.
2. LineNumber, ColumnNumber, EndLineNumber, EndColumnNumber: Int32 each.

Null/empty strings remain distinct. UTF-8 is strict. Limits are 1,024 warnings,
1 MiB per string, and 16 MiB for the warning section. Invalid bounds, negative
positions, malformed encoding, truncation, and trailing state are rejected
before restoration.

Only exact `BuildWarningEventArgs` instances are supported. Snapshot formatted
fields before asynchronous logging or policy conversion, not raw argument
objects. Replay regenerates task context, project association, timestamp,
and thread identity. Live replay preserves HelpLink; the existing binlog
encoding's omission of that field is unchanged.

## Capture and publication

The diagnostic watch spans initialization through cleanup. Replayable warning
capture covers only execution and referenced output getters on a cache miss.
Setup, binding, input-getter, cleanup, and unsupported diagnostics block caching.
Raw errors are observed before `ContinueOnError` conversion.

Capture each referenced getter once before cleanup, including getters whose
binding conditions are false. Publish after cleanup and input revalidation.
On a hit, validate state and restore files, then replay warnings before current
output binding. Current warning policy applies without entering the key.

See [BuildXL storage](task-cache-buildxl.md) for lifecycle and maintenance.
