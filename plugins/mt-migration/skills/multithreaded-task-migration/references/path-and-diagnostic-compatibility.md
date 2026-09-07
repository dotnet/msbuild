# Path and diagnostic compatibility

Use this reference when a migration changes how paths are resolved or carried through helpers. Preserve the requested behavior, not merely successful execution.

## Establish the baseline

For each reachable input, record its original value, filesystem interpretation, consumer, and failure handling. Observable behavior includes `Execute()` results, outputs, diagnostic codes/arguments, exception types, file locations/content, and whether processing continues after one failed item.

An observable difference is a compatibility question requiring evidence. It is not automatically a defect: the user may have authorized a behavior change, and the input or host may be outside the supported contract.

In the inspected `AbsolutePath` implementation:

- `Value` carries the I/O path; `OriginalValue` carries the input used to construct it.
- Implicit string conversion and `ToString()` return `Value`, not `OriginalValue`.
- Construction against a base combines/anchors paths without general dot-segment normalization.
- `GetCanonicalForm()` applies `Path.GetFullPath` and preserves `OriginalValue`.
- `default(AbsolutePath)` has no usable path value. It is not a success-shaped fallback for failed conversion.
- The null/empty guard uses `ArgumentException.ThrowIfNullOrEmpty`; distinguish null and empty exception types when the existing contract does.

Check these details against the target assembly, especially its runtime-specific implementation. Do not replace the consumer's existing path comparer or validation policy just because the wrapper has its own equality behavior.

## 1. Output property contamination

An implicit conversion can change an output from the user's relative spelling to an absolute path:

```csharp
AbsolutePath output = TaskEnvironment.GetAbsolutePath(relativeOutput);
OutputFile = output; // Changes the public value if it previously stayed relative.
```

When the old contract preserves the supplied representation:

```csharp
AbsolutePath output = TaskEnvironment.GetAbsolutePath(relativeOutput);
OutputFile = output.OriginalValue;
File.WriteAllText(output, contents);
```

If the task derives a new filename, first construct that original-form filename and then resolve it for I/O. Do not assign the original parent-directory input as though it were the newly computed output.

Trace consumers too: project properties/items, subsequent tasks, comparisons, serialization, and generated XML can all observe the spelling.

## 2. Diagnostic and exception path inflation

Passing `AbsolutePath` to a logging argument normally exposes `Value`. Use the original-form value when that is what the pre-migration diagnostic displayed.

Do not invent a new diagnostic or change an MSB code to accommodate the migration. Reuse the task's resource-based diagnostic and argument order.

Indirect leakage is equally important: a helper can receive the absolute path and then throw an exception whose `FileName` or `Message` contains it. Trace the throw and the actual reporting path.

Prefer structured original-path arguments where the existing diagnostic supports them. Do **not** introduce a broad `catch (Exception)` or blindly replace substrings in `ex.Message`: that can hide an unrelated exception, corrupt unrelated text, alter localization, or lose useful details. If message translation is genuinely required, scope it to the known failure and preserve the established contract.

An already-absolute original input, an intentionally absolute diagnostic, or an unreachable failure path does not justify an automatic blocker.

## 3. Null coalescing that changes control flow

Adding `?? string.Empty`, a new default directory, or an early success return can bypass an error/default path that existed before.

```csharp
string directory = Path.GetDirectoryName(input) ?? string.Empty;
```

Ask what the next operation previously did when `directory` was null. Did the task reject it, catch an exception, skip the item, or use a deliberate default? Do not assume that making `Path.Combine` accept a new value is a harmless safety improvement.

Root paths, an empty input, and a filename without a directory can differ by API/runtime. Read the caller's guards and establish behavior for the supported TFMs rather than copying a universal return-value table.

## 4. Conversion and catch-scope mismatch

A catch block may need the resolved path for I/O, such as querying locks, while logging the original input. Conversely, moving `GetAbsolutePath` outside an existing try can make conversion failures escape the task's normal error handling.

There is no universal "hoist above try" fix. Keep conversion within the required exception boundary, retain the successfully resolved value where later I/O needs it, and handle conversion failure through the existing failure path.

Do not retry the same failed conversion in a catch, use the unresolved input for filesystem probes, or treat `default(AbsolutePath)` as a valid path. Trace cleanup and cancellation paths as well as the main operation.

## 5. Canonicalization mismatch

Absolutizing is not the same as preserving the old canonical representation.

```csharp
AbsolutePath absolute = TaskEnvironment.GetAbsolutePath(input);
AbsolutePath canonical = absolute.GetCanonicalForm();
```

Use the canonical form only where the old algorithm required it: dictionary/set keys, deduplication, comparisons, or explicitly normalized outputs. Do not normalize all diagnostics or outputs merely because a canonical form is available.

Check dot segments, separators, trailing separators, case comparison, and aliases under the actual platform/consumer contract. Normalizing the path but changing the comparer can still change behavior.

## 6. Exception type, timing, and batch behavior changes

`File.Exists`-style probes and parsing/loading APIs do not all reject inputs the same way. Earlier conversion can replace a false result or a caught I/O exception with an argument exception before the old operation runs.

For each conversion, compare:

1. Existing null/empty/optional-input guards.
2. The old API's failure and the catch/filter that handled it.
3. The new conversion's failure type and location.
4. The resulting task return value, diagnostic, outputs, and remaining batch work.

If one bad item previously did not abort later items, preserve that behavior. If it previously failed the task, do not turn it into a logged message followed by success. Narrowing a catch is itself a behavioral change; do it only with a demonstrated contract and appropriate scope.

## 7. Rooted is not always fully qualified

On Windows, `C:input.txt` and `\input.txt` are rooted forms that still depend on drive context. A `Path.IsPathRooted` short-circuit is not sufficient proof of process independence.

The inspected `AbsolutePath(path, basePath)` implementation anchors these Windows forms to the provided base and deliberately leaves general normalization to `GetCanonicalForm`. Its runtime-specific branches must be checked for the assembly being consumed.

Resolve filesystem inputs through the supported environment API where required, without bypassing it based on an insufficient rootedness check. Preserve earlier validation: do not blindly convert every nullable or unused string, and do not apply Windows path assumptions to Unix inputs.

## 8. Swallowed failures that become incorrect semantic answers

A catch can mean "not an assembly," "not found, use default," or "unsupported, skip," rather than "fail the task." Resolving against the wrong directory can then produce a false negative with a successful build.

An illustrative probe, whose existing contract permits a missing optional file:

```csharp
try
{
    AssemblyName identity = AssemblyName.GetAssemblyName(candidatePath);
    InspectIdentity(identity);
}
catch (FileNotFoundException)
{
    // Existing optional-file behavior; not evidence that the intended file was inspected.
}
```

If `candidatePath` is project-relative but is resolved against a different process directory, the probe can be skipped even though the intended file exists. Resolve the input within the appropriate existing exception boundary and assert the semantic result.

A catch for `BadImageFormatException` alone does **not** establish that a missing-file failure was swallowed. Identify the actual thrown exception, matching handler, or wrong-directory file that satisfies the failure path. Do not repeat an anecdote with an exception mechanism the source does not support.

Search every relevant use of the input, not just those already converted. Correct resolution in later checks does not repair an earlier unresolved metadata/XML/archive/certificate probe.

## Select a relevant compatibility matrix

| Dimension | Cases to consider when supported/reachable |
| --- | --- |
| Input | Relative, absolute, null, empty, whitespace-only, root, dot segments, trailing separator |
| Platform | Drive-relative/root-relative Windows paths, UNC, separators, case, long paths |
| Result | Return value, exact outputs, diagnostic identity/arguments, exception type |
| Side effect | Correct file/content, no write in a decoy directory, remaining batch items |
| Interpretation | A semantic answer changed by missing/wrong input, not only success/failure |

Select cases from the changed contract; the table is not a claim that every task needs every scenario. Compare the baseline and candidate under the same input/host conditions.

## Source landmarks

Resolve `src\Framework\PathHelpers\AbsolutePath.cs` (`Value`, `OriginalValue`, constructors, `GetCanonicalForm`) and `src\Framework\TaskEnvironment.cs` in the reviewed implementation. Then follow the affected task's real diagnostics, exception helpers, and output consumers. Source-derived conclusions and runtime observations must remain distinguishable.
