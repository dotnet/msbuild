---
name: optimizing-msbuild-performance
description: 'Guides performance optimization for MSBuild engine code. Consult when working on hot paths in evaluation or execution, reducing allocations, choosing collection types, handling strings efficiently, modifying Expander.cs or Evaluator.cs, using Span<T>/stackalloc, caching values, or profiling build performance. Also applies when reviewing PRs for performance regression.'
argument-hint: 'Describe the performance-sensitive code area or optimization goal.'
---

# MSBuild Performance Guidelines

MSBuild evaluates and builds thousands of projects in enterprise solutions. Performance is an architectural concern, not an afterthought.

## Guiding Principle

**Profile before optimizing; measure, do not guess.** Use BenchmarkDotNet, the evaluation profiler (`/profileevaluation`), or ETW traces to identify actual bottlenecks before optimizing.

See [evaluation-profiling.md](../../../documentation/evaluation-profiling.md) and [General_perf_onepager.md](../../../documentation/specs/proposed/General_perf_onepager.md) for profiling tools.

## Allocation Awareness on Hot Paths

The evaluation and execution engines process millions of operations per build. Unnecessary allocations cause GC pressure that compounds across large solutions.

### Rules

1. **Avoid LINQ in hot paths.** `Where`, `Select`, `Any`, `First` all allocate enumerator objects and delegate closures. Use `foreach` loops instead.

   ```csharp
   // BAD — allocates iterator + delegate
   var match = items.FirstOrDefault(i => i.Name == name);

   // GOOD — zero allocations
   foreach (var item in items)
   {
       if (item.Name == name) { /* found */ break; }
   }
   ```

2. **Avoid string allocations in formatting.** Use `string.Concat`, interpolation with `Span<T>`, or `StringBuilder` reuse — not `string.Format` on hot paths.

3. **Prefer `Span<T>` and `stackalloc` for short-lived buffers** when parsing or slicing strings. Avoid `Substring` when you only need to compare or inspect a portion.

4. **Cache computed values.** If a value is computed in a loop but doesn't change, hoist it out. If it's expensive and reused across calls, use a field or `Lazy<T>`.

## String Comparison Rules

MSBuild property, item, and target names are **case-insensitive**. Getting this wrong causes subtle bugs and perf issues.

| Scenario | Use |
|----------|-----|
| Property/item/target names | `MSBuildNameIgnoreCaseComparer` |
| General MSBuild identifiers | `StringComparison.OrdinalIgnoreCase` |
| Dictionary keys for MSBuild names | `MSBuildNameIgnoreCaseComparer` as comparer |
| File paths on Windows | `StringComparison.OrdinalIgnoreCase` |
| File paths on Linux | `StringComparison.Ordinal` |

**Never** use `ToLower()`/`ToUpper()` for comparisons — they allocate a new string every time.
**Never** use `CurrentCulture` for MSBuild identifiers — build behavior must not vary by locale.

## Collection Type Selection

| Access Pattern | Recommended Type |
|---------------|-----------------|
| Small fixed set (< ~8 items) | Array or `ReadOnlySpan<T>` |
| Build-once, read-many | `ImmutableArray<T>` (not `ImmutableList<T>`) |
| Keyed lookup, many items | `Dictionary<TKey, TValue>` with appropriate comparer |
| Keyed lookup, few items (< 5) | Linear scan over array (cache-friendly, avoids dict overhead) |
| Concurrent reads, rare writes | `ImmutableDictionary` or snapshot pattern |
| Ordered iteration needed | `List<T>` or array; avoid `HashSet<T>` if order matters |

**`ImmutableList<T>` is almost never the right choice** — it has O(log n) access vs O(1) for `ImmutableArray<T>`.

## Hot Path Identification

These areas are performance-critical and require extra scrutiny:

- **`Expander.cs`** — property/item/metadata expansion during evaluation
- **`Evaluator.cs`** — project evaluation orchestration
- **`ItemSpec.cs` / `LazyItemEvaluator.cs`** — item evaluation and globbing
- **`TaskExecutionHost.cs`** — task parameter marshaling
- **File I/O paths** — `FileMatcher.cs`, glob operations, project loading

### Inlining Considerations

For extremely hot methods, consider:
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small methods called millions of times
- Avoiding virtual dispatch in inner loops
- Using `struct` enumerators to avoid heap allocation

## Evaluation Performance Specifics

- **Import chain depth** affects evaluation time linearly. Minimize unnecessary imports.
- **Glob patterns** are evaluated per-project. Overly broad globs (`**/*`) are expensive.
- **Condition evaluation** should use short-circuit logic — put cheap checks first.
- **Property functions** (`$([System.IO.Path]::...)`) are interpreted and slower than built-in operations.

## Anti-Patterns

| Anti-Pattern | Why It's Bad | Fix |
|-------------|-------------|-----|
| `items.Count() > 0` | Enumerates entire collection | `items.Any()` or check `.Count` property |
| `string.Format` in log messages at Low importance | Allocates even if message is filtered | Use structured logging or guard with verbosity check |
| `new List<T>(enumerable).ToArray()` | Double allocation | `enumerable.ToArray()` directly |
| `dict.ContainsKey(k) then dict[k]` | Double lookup | `dict.TryGetValue(k, out var v)` |
| Regex in a loop without `RegexOptions.Compiled` | Reinterprets pattern each time | Compile or use static `Regex` field |
