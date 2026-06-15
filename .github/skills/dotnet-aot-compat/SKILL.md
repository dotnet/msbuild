---
name: dotnet-aot-compat
description: >
  Make .NET projects compatible with Native AOT and trimming by systematically
  resolving IL trim/AOT analyzer warnings. USE FOR: making projects AOT-compatible,
  fixing trimming warnings, resolving IL warnings (IL2026, IL2070, IL2067, IL2072,
  IL3050), adding DynamicallyAccessedMembers annotations, enabling IsAotCompatible.
  DO NOT USE FOR: publishing native AOT binaries, optimizing binary size, replacing
  reflection-heavy libraries with alternatives.
  INVOKES: no tools — pure knowledge skill.
license: MIT
---

# dotnet-aot-compat

Make .NET projects compatible with Native AOT and trimming by systematically resolving all IL trim/AOT analyzer warnings.

## When to Use This Skill

- **"Make this project AOT-compatible"**
- **"Fix trimming warnings"** or **"fix IL warnings"**
- **"Resolve IL2070 / IL2067 / IL2072 / IL2026 / IL3050 warnings"**
- **"Add DynamicallyAccessedMembers annotations"**
- **"Enable IsAotCompatible in my .csproj"**
- **"My project has trim analyzer warnings after upgrading to net8.0"**
- **"Annotate reflection code for the trimmer"**

## When Not to Use This Skill

Do not use this skill when the project exclusively targets .NET Framework (net4x), which does not support the trim/AOT analyzers.

## Prerequisites

An existing .NET project targeting net8.0 or later (or multi-targeting with at least one net8.0+ TFM) and the corresponding .NET SDK installed.

## Background: What AOT Compatibility Means

Native AOT and the IL trimmer perform static analysis to determine what code is reachable. Reflection can break this analysis because the trimmer can't see what types/members are accessed at runtime. The `IsAotCompatible` property enables analyzers that flag these issues as build warnings (ILXXXX codes).

## Critical Rules

### ❌ Never suppress warnings incorrectly

- **NEVER** use `#pragma warning disable` for IL warnings. It hides warnings from the Roslyn analyzer at build time, but the IL linker and AOT compiler still see the issue. The code will fail at trim/publish time.
- **NEVER** use `[UnconditionalSuppressMessage]`. It tells both the analyzer AND the linker to ignore the warning, meaning the trimmer cannot verify safety. Raising an error at build time is always preferable to hiding the issue and having it silently break at runtime.

### 💡 Preferred approaches

- **Prefer** `[DynamicallyAccessedMembers]` annotations to flow type information through the call chain.
- **Prefer** refactoring to eliminate patterns that break annotation flow (e.g., boxing `Type` through `object[]`).
- **Use** `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` / `[RequiresAssemblyFiles]` to mark methods as fundamentally incompatible with trimming, propagating the requirement to callers. This surfaces the issue clearly rather than hiding it — callers must explicitly acknowledge the incompatibility.

### Annotation flow is key

The trimmer tracks `[DynamicallyAccessedMembers]` annotations through assignments, parameter passing, and return values. If this flow is broken (e.g., by boxing a `Type` into `object`, storing in an untyped collection, or casting through interfaces), the trimmer loses track and warns. The fix is to preserve the flow, not suppress the warning.

## Step-by-Step Procedure

> **Do not explore the codebase up-front.** The build warnings tell you exactly which files and lines need changes. Follow a tight loop: **build → pick a warning → open that file at that line → apply the fix recipe → rebuild**. Reading or analyzing source files beyond what a specific warning points you to is wasted effort and leads to timeouts. Let the compiler guide you.
>
> ❌ Do NOT run `find`, `ls`, or `grep` to understand the project structure before building. Do NOT read README, docs, or architecture files. Your first action should be Step 1 (enable AOT analysis), then build.

### Step 1: Enable AOT analysis in the .csproj

Add `IsAotCompatible`. If the project doesn't exclusively target net8.0+, add a TFM condition (AOT analysis requires net8.0+):

```xml
<PropertyGroup>
  <IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>
</PropertyGroup>
```

This automatically sets `EnableTrimAnalyzer=true` and `EnableAotAnalyzer=true` for compatible TFMs. For multi-targeting projects (e.g., `netstandard2.0;net8.0`), the condition ensures no `NETSDK1210` warnings on older TFMs.

### Step 2: Build and collect warnings

```bash
dotnet build <project.csproj> -f <net8.0-or-later-tfm> --no-incremental 2>&1 | grep 'IL[0-9]\{4\}'
```

Sort and deduplicate. Common warning codes:
- **IL2070**: Reflection call on a `Type` parameter missing `[DynamicallyAccessedMembers]`
- **IL2067**: Passing an unannotated `Type` to a method expecting `[DynamicallyAccessedMembers]`
- **IL2072**: Return value or extracted value missing annotation (often from unboxing)
- **IL2057**: `Type.GetType(string)` with a non-constant argument
- **IL2026**: Calling a method marked `[RequiresUnreferencedCode]`
- **IL2050**: P/invoke method with COM marshalling parameters
- **IL2075**: Return value flows into reflection without annotation
- **IL2091**: Generic argument missing `[DynamicallyAccessedMembers]` required by constraint
- **IL3000**: `Assembly.Location` returns empty string in single-file/AOT apps
- **IL3050**: Calling a method marked `[RequiresDynamicCode]`

### Step 3: Triage warnings by code (do NOT read every file)

Group the warnings from Step 2 by warning code and count them. **Do not open individual files yet.** Identify the top 1-2 patterns by count — these drive your fix strategy:

| Pattern | Typical fix |
|---------|-------------|
| Many IL2026 + IL3050 from `JsonSerializer` | **Go to Strategy C immediately** — create a `JsonSerializerContext`, then batch-update all call sites |
| IL2070/IL2087 on `Type` parameters | Add `[DynamicallyAccessedMembers]` to the innermost method, then cascade outward |
| IL2067 passing unannotated `Type` | Annotate the parameter at the source |

**In most real projects, IL2026/IL3050 from JsonSerializer dominate.** Start with Strategy C unless the warning breakdown clearly shows otherwise. After the batch JSON fix, handle remaining warnings with Strategies A–B. Only use Strategy D as a last resort.

### Step 4: Fix warnings iteratively (innermost first)

Work from the **innermost** reflection call outward. Each fix may cascade new warnings to callers.

**Stay warning-driven.** For each warning, open only the file and line the compiler reported, identify the pattern, apply the matching fix recipe below, and move on. Do not scan the codebase for similar patterns or try to understand the full architecture — fix what the compiler tells you, rebuild, and let new warnings guide the next change. Fix a small batch of warnings (5-10), then rebuild immediately to check progress.

**Use sub-agents when available.** If you can launch sub-agents (e.g., via a `task` tool), dispatch **multiple sub-agents in parallel** to edit different files simultaneously. Keep the main loop focused on building, parsing warnings, and dispatching — delegate actual file edits to sub-agents. For batch JSON updates, give each sub-agent 5-10 files to update in one prompt. **After 2 build-fix cycles, dispatch all remaining file edits to sub-agents in parallel — do not continue fixing files sequentially.** Example:

> Update these files to use source-generated JSON: `src/Models/Resource.Serialization.cs`, `src/Models/Identity.Serialization.cs`, `src/Models/Plan.Serialization.cs`. In each file, replace `JsonSerializer.Serialize(writer, value)` with `JsonSerializer.Serialize(writer, value, MyProjectJsonContext.Default.TypeName)` and `JsonSerializer.Deserialize<T>(ref reader)` with `JsonSerializer.Deserialize(ref reader, MyProjectJsonContext.Default.TypeName)`. Only edit the JsonSerializer call sites.

#### Strategy A: Add `[DynamicallyAccessedMembers]` (preferred)

When a method uses reflection on a `Type` parameter, annotate the parameter to tell the trimmer what members are needed:

```csharp
using System.Diagnostics.CodeAnalysis;

// Before (warns IL2070):
void Process(Type t) {
    var method = t.GetMethod("Foo");  // trimmer can't verify
}

// After (clean):
void Process([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type t) {
    var method = t.GetMethod("Foo");  // trimmer preserves public methods
}
```

When you annotate a parameter, **all callers** must now pass properly annotated types. This cascades outward — follow each caller and annotate or refactor as needed. **The caller's annotation must include at least the same member types as the callee's.** If the callee requires `PublicConstructors | NonPublicConstructors`, the caller must specify the same or a superset — using only `NonPublicConstructors` will produce IL2091.

#### Strategy B: Refactor to preserve annotation flow

When annotation flow is broken by boxing (storing `Type` in `object`, `object[]`, or untyped collections), **refactor** to pass the `Type` directly:

```csharp
// BROKEN: Type boxed into object[], annotation lost
void Process(object[] args) {
    Type t = (Type)args[0];  // IL2072: annotation lost through boxing
    Evaluate(t, ...);
}

// FIXED: Pass Type as a separate, annotated parameter
void Process(
    object[] args,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type calleeType,
    ...) {
    Evaluate(calleeType, ...);  // annotation flows cleanly
}
```

Common patterns that break flow and how to fix them:
- **`object[]` parameter bags**: Extract the `Type` into a dedicated annotated parameter
- **Dictionary/List storage**: Use a typed field with annotation instead
- **Interface indirection**: Add annotation to the interface method's parameter
- **Property with boxing getter**: Annotate the property's return type

#### Strategy C: Source-generated JSON serialization (batch fix)

When most warnings are IL2026/IL3050 from `JsonSerializer.Serialize`/`Deserialize`, this is a single mechanical fix applied in bulk:

1. **Collect affected types** — grep for all `JsonSerializer.Serialize` and `JsonSerializer.Deserialize` call sites. Extract the type being serialized (the `<T>` in `Deserialize<T>`, or the runtime type of the object in `Serialize`).

2. **Create one `JsonSerializerContext`** with `[JsonSerializable]` for every type found. **Skip types from external packages** (e.g., `ResponseError` from `Azure.Core`) — they won't source-generate for types you don't own. Handle external types separately via Gotcha #1 below.

```csharp
[JsonSerializerContext]
[JsonSerializable(typeof(ManagedServiceIdentity))]
[JsonSerializable(typeof(SystemData))]
// ... one attribute per type YOU OWN
// Do NOT add types from external packages (e.g., ResponseError)
internal partial class MyProjectJsonContext : JsonSerializerContext { }
```

3. **Batch-update all call sites** — do not read each file individually. Apply the pattern mechanically:
   - `JsonSerializer.Serialize(obj)` → `JsonSerializer.Serialize(obj, MyProjectJsonContext.Default.TypeName)`
   - `JsonSerializer.Deserialize<T>(json)` → `JsonSerializer.Deserialize(json, MyProjectJsonContext.Default.TypeName)`

   Find and update all call sites in one pass:
   ```bash
   # Find all files with JsonSerializer calls
   grep -rl 'JsonSerializer\.\(Serialize\|Deserialize\)' src/ --include='*.cs'
   ```
   Then use sequential `edit` calls to apply the same transformation to every matching file. **Do not use `sed` for C# code** — generics like `Deserialize<T>()` have angle brackets and nested parentheses that sed will mangle.

4. **Build once** to verify. Remaining warnings will be non-serialization issues — handle those with Strategies A–B or D.

#### Strategy D: `[RequiresUnreferencedCode]` (last resort)

When a method fundamentally requires arbitrary reflection that cannot be statically described:

```csharp
[RequiresUnreferencedCode("Loads plugins by name using Assembly.Load")]
public void LoadPlugin(string assemblyName) {
    var asm = Assembly.Load(assemblyName);
    // ...
}
```

This propagates to callers — they must also be annotated with `[RequiresUnreferencedCode]`. Use sparingly; it marks the entire call chain as trim-incompatible.

### Step 5: Rebuild and repeat

After each small batch of fixes (5-10 warnings), rebuild with `--no-incremental` and check for new warnings. **Do not attempt to fix all warnings before rebuilding** — frequent rebuilds catch mistakes early and reveal cascading warnings. Fixes cascade — annotating an inner method may surface warnings in its callers. Repeat until `0 Warning(s)`.

### Step 6: Validate all TFMs

Build all target frameworks to ensure:
- **0 IL warnings** on net8.0+ TFMs
- **No NETSDK1210 warnings** (the `IsAotCompatible` condition handles this)
- **Clean builds** on older TFMs (netstandard2.0, net472, etc.)

```bash
dotnet build <project.csproj>  # builds all TFMs
```

## Stop Signals

- **Do not analyze more than 2-3 representative files per warning pattern.** After identifying the fix for a pattern, apply it to all matching files without reading each one first.
- **Start fixing after one build.** Do not do a second analysis pass — begin implementing fixes for the most common warning pattern immediately after Step 3 triage.
- Stop after achieving **0 IL warnings** for net8.0+ TFMs. Don't optimize or refactor already-clean annotations.
- If a warning requires **architectural refactoring** beyond annotation flow fixes (e.g., replacing an entire serialization layer), document it and stop — don't rewrite large subsystems.
- Limit to **3 build-fix iterations** per warning. If annotation flow doesn't resolve it after 3 attempts, escalate to `[RequiresUnreferencedCode]`.
- Don't chase warnings in **third-party dependencies** you can't modify. Note them and move on.
- If the user asked a scoped question (e.g., "fix warnings in this file"), don't expand to the entire project.

## Polyfills for Older TFMs

For multi-targeting projects that include netstandard2.0 or net472, you need polyfills for `DynamicallyAccessedMembersAttribute` and related types. See [references/polyfills.md](references/polyfills.md).

## Common Gotchas

1. **External types without AOT-safe serialization**: When a type comes from a dependency you can't modify (e.g., `ResponseError` from `Azure.Core`) and it lacks a source-generated serializer, `Options.GetConverter<T>()` is reflection-based and will produce IL warnings. First check if the type implements `IJsonModel<T>` (common in Azure SDK) — if so, bypass `JsonSerializer` entirely:

```csharp
// Before (IL2026 — JsonSerializer uses reflection):
JsonSerializer.Serialize(writer, errorValue);

// After (AOT-safe — uses IJsonModel directly):
((IJsonModel<ResponseError>)errorValue).Write(writer, ModelReaderWriterOptions.Json);

// For deserialization:
var error = ((IJsonModel<ResponseError>)new ResponseError()).Create(ref reader, ModelReaderWriterOptions.Json);
```

Do **not** add the external type to your `JsonSerializerContext` — it won't source-generate for types you don't own. If the type doesn't implement `IJsonModel<T>`, write a custom `JsonConverter<T>` with manual `Utf8JsonReader`/`Utf8JsonWriter` logic and register it via `[JsonSourceGenerationOptions]` on your context.

2. **Serialization libraries**: Most reflection-based serializers (e.g., `Newtonsoft.Json`, `XmlSerializer`) are not AOT-compatible. Migrate to a source-generation-based serializer such as `System.Text.Json` with a `JsonSerializerContext`. If migration is not feasible, mark the serialization call site with `[RequiresUnreferencedCode]`.

3. **Shared projects / projitems**: When source is shared between multiple projects via `<Import>`, annotations added to shared code affect ALL consuming projects. Verify that all consumers still build cleanly.

## References

[Limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=windows%2Cnet8#limitations-of-native-aot-deployment)
[Conceptual: Understanding trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-concepts)
[How-to: trim compat](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings)

## Checklist

- [ ] Added `<IsAotCompatible>` with TFM condition to .csproj
- [ ] Built with AOT analyzers enabled (net8.0+ TFM)
- [ ] Fixed all IL warnings via annotations or refactoring
- [ ] No `#pragma warning disable` or `[UnconditionalSuppressMessage]` used for any IL warning
- [ ] Polyfills present for older TFMs if needed
- [ ] All target frameworks build with 0 warnings
- [ ] Verified shared/linked source doesn't break sibling projects
