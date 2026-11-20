# Precompute Compute-Phase Properties and Items

## Status

Draft.

This specification describes how properties and items in targets are expanded when some of their values are only available during the compute phase.

## Feature Design

### Background

Compute work often produces properties and items that later work consumes:

- a compute task writes properties and items through `<Output />`;
- a property or item operation inside a target is assigned to the compute phase, for example to glob files that only exist during compute.

Tasks and property and item operations in compute work are expanded the same way, in two passes.
Precompute expands everything it can and leaves references to compute-phase values in place.
Compute expands the result again with the normal code once compute-phase values are available.

### Existing Behavior

After evaluation, the bodies of targets are stored as raw text.
When a target runs, each property and item element is expanded against the build's current properties and items and the result is written back.
Expansion and execution happen together, in one process, so no expression or partial result ever needs to be saved.

### Compute-Phase Properties and Items

A compute-phase property or item is one whose value is produced during compute.
A property or item becomes compute-phase when compute work writes it:

- a compute task writes it through `<Output />`;
- an operation inside a target marked `Phase="Compute"` writes it.

```xml
<Target Name="Collect">
  <GenTask Phase="Compute">
    <Output TaskParameter="Manifest" PropertyName="GenManifest" />
  </GenTask>
  <ItemGroup>
    <Published Phase="Compute" Include="$(PublishDir)**\*" />
  </ItemGroup>
</Target>
```

`Phase` on a property or item element accepts `Precompute`, the default, and `Compute`.
It is only supported on elements inside a target; work that must observe compute results moves into a target.
The phase is never inferred: an operation that references a compute-phase value must itself be marked `Phase="Compute"`, so each error points at the operation that needs a decision.

`Phase` only has meaning when precompute is enabled:

- On an item element, a `Phase` attribute is parsed as metadata, as it is today.
  Normal builds keep that metadata; precompute treats the attribute as the phase and does not set the metadata.
- On a property element, a `Phase` attribute is accepted and ignored by normal builds.
  Property elements reject unknown attributes today, so this does not change any valid project.

A property or item can start as precompute and later become compute-phase.
Its value at that point, its *original precompute value*, carries into the compute phase:

```xml
<Foo Include="X" />
...
<Foo Phase="Compute" Include="@(SomeComputeItem)" />
```

Here the compute value of `Foo` is `X` followed by the items of `SomeComputeItem`.
This supports SDK patterns that add compute results to established item types such as `ReferencePath`.

### Precompute References

Once a property or item is compute-phase, precompute can no longer reference it, because its value does not exist yet.
Referencing it from any of the following is an error:

- precompute tasks and precompute property and item operations;
- target conditions, `DependsOnTargets`, `Inputs`, `Outputs`, and other target attributes;
- batching;
- `MSBuild` and `CallTarget` parameters, and target `Returns`.

A property or item name that depends on a compute-phase value, such as `@($(CFoo))`, is also an error, because the graph must know every name it reads and writes.

### Precompute Substitutions

Precompute runs targets as a normal build does.
Every precompute task and property and item operation runs immediately, so graph-shaping state, such as the `Compile` items that feed a target's `Inputs`, is fully known when the graph is built.

Compute tasks and compute operations are captured into the target's compute node instead of running.

### Two-Pass Expansion

Each attribute of compute work is expanded twice:

- **Pass 1 (precompute)** runs the normal expander and substitutes precompute values.
  References to compute-phase values are left as text, together with any transform, item function, separator, or property function applied to them.
- **Pass 2 (compute)** runs the normal expander, condition evaluator, intrinsic task, or task parameter binding on the pass 1 result, with the compute-phase values the node imports.

| Expression in compute work | Pass 1 result |
| --- | --- |
| `$(PFoo)` | PFoo's value if it is plain, otherwise `$(__PFoo)` |
| `$(CFoo)` | `$(CFoo)` |
| `$(PFoo.ToUpper())` | the function's result if it is plain, otherwise a temporary holding it |
| `$([MSBuild]::NormalizeDirectory($(CFoo)))` | unchanged |
| `$([MSBuild]::NormalizeDirectory($(CFoo), $(PFoo)))` | `$([MSBuild]::NormalizeDirectory($(CFoo), $(__PFoo)))`, with `__PFoo` holding PFoo's value |
| `@(PFoo)` | `@(__PFoo)`, a snapshot of PFoo's items with metadata |
| `@(CFoo)` | `@(CFoo)` |
| `@(PFoo);@(CFoo)` | `@(__PFoo);@(CFoo)` |
| `@(PFoo->'%(Filename).obj')` | `@(__PFoo)`, holding the transformed items with the metadata they copy from PFoo's items |
| `@(CFoo->'$(PFoo)\%(Filename)')` | `@(CFoo->'pfoo\%(Filename)')` if PFoo is plain, otherwise `@(CFoo->'$(__PFoo)\%(Filename)')` |
| `@(CFoo->Count())`, `@(CFoo, ',')` | unchanged |
| `$(PFoo)` whose value is `@(PBar)` | `$(__PFoo)`, holding `@(__PBar)`, a snapshot of PBar, as the item pass would find it in a normal build |
| `$(CFoo)` whose original precompute value is `@(PBar)` | `$(CFoo)`; the original value is stored as `@(__PBar)`, so pass 2 expands it to PBar's items if no compute write replaces it, such as when a conditional compute write's condition is false |
| `%(PFoo.Culture)` in a precompute batch | the bucket's value, with `%` escaped |
| `$(OutDir)gen\*.cs` | `bin\gen\*.cs`; the wildcard is expanded in pass 2 |
| `@($(CFoo))` | error; item type names must be known during precompute |

Here `PFoo` and `PBar` are precompute properties or items and `CFoo` is compute-phase.

Pass 2 is the normal code on equivalent text, so expansion during compute behaves as in a normal build, including property functions, transforms, item functions, wildcards, and item syntax in property values.

Anything pass 1 evaluates sees the file system as it is during precompute, as precompute tasks do.
Work that must observe files produced during compute, such as `Exists()` or `%(ModifiedTime)` on generated files, belongs in a compute property or item, which pass 2 evaluates.

#### Temporaries

Pass 2 rescans the pass 1 text, which a normal build never does with a substituted value, so pass 1 writes a precompute value as text only when pass 2 cannot read syntax in it:

1. A precompute property value is written as text if it is plain: it contains none of `$`, `%`, `@`, or `'`, and does not start with `(`.
   Any other value is stored in the node as a temporary compute-phase property, and pass 1 writes a reference to it.
   Pass 2 substitutes the temporary once, at the stage a normal build substitutes the value, so every character keeps the meaning it has in a normal build.
2. Every precompute property in a property function left for pass 2 is a temporary, because a normal build parses the function before substituting values.
3. Metadata from a precompute batch is written as text, with `%` written as `%25` unless it starts a `%XX` escape, and in a condition `'` written as `%27`.
   A normal build expands properties and items after metadata, as pass 1 does, and never reads a metadata value's `%`, or in a condition its `'`, as syntax.
4. An item expression of precompute items with no compute-phase reference or temporary is evaluated in pass 1, including its transforms and item functions, and replaced by a temporary item type holding its result items, so their metadata survives pass 2.
   In any other item expression, a precompute item type is replaced by a snapshot of its items.
5. A temporary property whose value contains item syntax has its precompute item types replaced by snapshots, so pass 2 expands them as the item pass of a normal build would at that point.
   This includes the original precompute value of a property that becomes compute-phase.

A temporary is named `__P` followed by the property or item it holds, such as `__PSchema` for `Schema`, with a suffix if the same name is captured more than once in a node.
A temporary captures the value at the point it is read, so a later change to the same name, including the name becoming compute-phase, does not affect it.

### Conditions

The phase of the whole element determines when its condition is evaluated and when its wildcards are expanded.

- A precompute element's condition is evaluated during precompute; reading a compute-phase value is an error.
- A compute element's condition goes through pass 1.
  If the result reads no compute-phase value, it is evaluated during precompute: false drops the element, true removes the condition.
  Otherwise the whole condition is evaluated during compute, with normal short-circuit semantics.

### Compute Nodes and Values

Each target batch with compute work becomes a compute node.
Compute work is any compute task or compute property or item operation, so a target with compute operations but no compute task also becomes a node.
The node's steps are its compute tasks and operations in source order.

A node produces the compute-phase properties and items it writes, and consumes those it references.
It depends on the nodes that produce what it consumes.
Values flow between nodes in precompute order:

- a node that reads or writes a compute-phase property or item type imports its full value from the latest producer;
- a node that writes it exports its full value and becomes the latest producer.

A node that only adds items still imports and depends on the earlier value.
This adds dependencies a normal build does not need, but keeps the value flow simple.

A possible refinement is to skip the import for a node that only adds.
Pass 1 already scans each step for the compute-phase names it reads.
If no step in a node references `@(Type)` and none removes, modifies, or deduplicates `Type`, the node does not import it, its exported items are exactly the items it adds, and readers append them after the latest full value.
This restores independent producers using information precompute already collects, at the cost of a second kind of export.

A missing value is an error; a produced empty value is valid.

### Compute Execution

The node executor imports the compute-phase values the node consumes.
It initializes the node's temporaries, then runs each step's pass 2: it evaluates the remaining condition and runs the task or the property or item operation with the normal code.
Items created during compute receive the project's item definitions, captured during precompute.
The node then exports the values it produces.

### Example

```xml
<Target Name="Generate">
  <PropertyGroup>
    <GenDir>$(OutDir)gen\</GenDir>
  </PropertyGroup>

  <GenTask Phase="Compute" Sources="@(Schema)" OutputDirectory="$(GenDir)">
    <Output TaskParameter="Manifest" PropertyName="GenManifest" />
  </GenTask>

  <ItemGroup>
    <Gen Phase="Compute" Include="$(GenDir)*.cs" Exclude="$(GenDir)skip.cs">
      <Origin>generated</Origin>
    </Gen>
    <GenRefs Phase="Compute" Include="@(Gen->'%(Filename).ref')" />
    <FileWrites Include="$(GenDir)gen.manifest" />
  </ItemGroup>
</Target>

<Target Name="Build" DependsOnTargets="Generate">
  <Csc Phase="Compute"
       Condition="'$(GenManifest)' != ''"
       Sources="@(Compile);@(Gen)"
       References="@(GenRefs)"
       OutputAssembly="$(OutDir)app.dll" />
</Target>
```

`Schema` and `Compile` are precompute items from evaluation, and `OutDir` is `bin\`.

#### Precompute

`Generate` runs in source order:

1. `GenDir` is a precompute property, so it is set to `bin\gen\` immediately.
2. `GenTask` is a compute task, so it is captured.
   `OutputDirectory` becomes `bin\gen\`, and `Sources` becomes `@(__PSchema)`, a snapshot of `Schema`.
   Its `<Output />` makes `GenManifest` compute-phase.
3. `Gen` is a compute operation, so it is captured.
   Its wildcard is left for compute, because the generated files do not exist yet.
   `Gen` becomes compute-phase.
4. `GenRefs` transforms `Gen`, which is now compute-phase, so `@(Gen->'%(Filename).ref')` is left as text.
   `GenRefs` becomes compute-phase.
5. `FileWrites` is a precompute operation, so it runs immediately.
   If it referenced `@(Gen)`, it would be an error.

`Build` then captures `Csc`:

```text
Condition:      '$(GenManifest)' != ''
Sources:        @(__PCompile);@(Gen)      __PCompile = snapshot of Compile
References:     @(GenRefs)
OutputAssembly: bin\app.dll
```

The condition reads `GenManifest`, so it is evaluated during compute.

#### Graph

- `Generate` node
  - Temporaries: `__PSchema`.
  - Steps: `GenTask`, `Gen`, `GenRefs`.
  - Produces: `GenManifest`, `Gen`, `GenRefs`.
- `Build` node
  - Temporaries: `__PCompile`.
  - Steps: `Csc`.
  - Consumes: `GenManifest`, `Gen`, `GenRefs`.
  - Depends on: `Generate`.

`GenDir` and `FileWrites` appear in neither node; their values are already substituted where they are used.

#### Compute

The `Generate` node runs `GenTask` and sets `GenManifest`.
It expands `bin\gen\*.cs`, applies the exclude, and creates `Gen` items with `Origin` and the item definitions for `Gen`.
It transforms `Gen` into `GenRefs` and exports all three values.

The `Build` node imports them, evaluates its condition, expands each parameter, and runs `Csc`.

### Limitations

The following table summarizes what compute work supports when precompute is enabled:

| Feature | Supported | Notes |
| --- | --- | --- |
| Property functions, transforms, item functions on compute-phase values | Yes | Run in pass 2 by the normal expander |
| Wildcards in compute item operations | Yes | Expanded in pass 2, so they see files produced during compute |
| Compute item `Remove`, metadata-only modifications, and item modifiers | Yes | Pass 2 runs `ItemGroupIntrinsicTask` unchanged |
| Conditions reading compute-phase values | Yes | Evaluated whole during compute |
| Task and operation batching over precompute items | Yes | Resolved during precompute |
| Task and operation batching over compute-phase items only | Yes | Done in pass 2 |
| Batching over both precompute and compute-phase items | No | Buckets would mix values from both phases |
| Target batching over compute-phase items | No | Target batches are graph nodes, so their number must be known during precompute |
| Compute-phase values in task attributes other than parameters and `Condition` | No | Such as `ContinueOnError` |
| `Phase` on `PropertyGroup` and `ItemGroup` | No | Phase is set per operation |

When precompute is enabled, the unsupported cases are errors.
Target batching over compute-phase items could later be supported with a container node that expands during compute into one child node per batch.

Other differences from a normal build:

- A condition that reads a compute-phase value is evaluated whole during compute and is never partially evaluated during precompute, so it still produces a compute step even if a precompute part would make it false.
  The existing condition nodes only evaluate to true or false, and partial evaluation needs an unknown result.
  A possible improvement is to walk only the `and`, `or`, and `not` nodes of the parsed condition, evaluate subtrees that read no compute-phase value with the existing code, and drop the step if the result is false, keeping the original condition text otherwise.

### Considerations

- **Non-Precompute Compatibility**: Behavior and performance are unchanged when precompute is not enabled.
  This takes priority over the elegance of precompute syntax.
- **Precompute Compatibility**: Compute work produces the same results as a normal build except as listed above.
  Restrictions that only apply when precompute is enabled are acceptable when the patterns they reject are rare.
- **Code Sharing**: Both passes use the normal expansion, condition, intrinsic task, and task binding code; there is no second implementation.

## Implementation

### Compute-Phase State

`Lookup` records compute-phase property names and item types, and the precompute value each had when it became compute-phase.

`Lookup` also enforces precompute reference errors.
Reading a compute-phase property, or the items of a compute-phase item type, through `Lookup` is an error, except from pass 1.
This covers every precompute reader, including those that bypass `Expander`, such as `BatchingEngine` and `ItemGroupIntrinsicTask`.

The state exists only during precompute.
Outside precompute the check is a single null test.

### Pass 1

Pass 1 is the existing expander with a pass 1 mode that `Lookup`'s compute-phase state turns on:

- The property pass leaves a reference to a compute-phase property as its original text.
  A property function whose receiver or arguments read a compute-phase value is left as its original text, with precompute properties inside it replaced by temporaries.
  Any other precompute value is written as text if it is plain, and as a temporary otherwise.
- The metadata pass leaves metadata of compute-phase items as its original text.
- The item pass leaves an item expression over a compute-phase item type, or containing a compute-phase reference or temporary, as its text, with a precompute item type replaced by a snapshot.
  After the property pass, the only `$(` left is a compute-phase reference or a temporary, so this is read from the text.
  Any other item expression, including its transforms and item functions, is evaluated and replaced by a reference to a temporary holding its result items.

The property and item passes take a writer, `IExpansionWriter`, that decides what each part of the expression writes.
A normal build uses a writer that does exactly what the passes did before, so its code is unchanged.

The existing entry points keep their signatures and behavior outside pass 1.
Pass 1 reports property reads and writes to the existing tracking, such as `PropertiesUseTracker` and BuildCheck, where they are captured.

### Compute Steps

A compute step is the element's existing instance with its attributes replaced by their pass 1 results:

- property operations use `ProjectPropertyGroupTaskPropertyInstance`;
- item operations use `ProjectItemGroupTaskItemInstance`;
- tasks use `ProjectTaskInstance`, with each parameter's pass 1 result.

Each step keeps its condition, if it was not resolved during precompute, and its source locations.
Bucket metadata from precompute batching is substituted in pass 1, so steps carry no batching state.

The node payload holds its steps, its temporaries as `ProjectPropertyInstance` and `ProjectItemInstance` values, the compute-phase values it imports, an item definition table for the item types it creates, and the project's defining file.
All of these already implement `ITranslatable`.

The compute-phase names a step reads are found by scanning its pass 1 text for property and item references.
Over-approximating only adds a graph dependency.

### Compute Execution

During compute, the node builds a `Lookup` from its temporaries and imports.
It runs each step with the normal code: `PropertyGroupIntrinsicTask` or `ItemGroupIntrinsicTask` for property and item operations, and normal parameter expansion and binding for tasks.
Items created during compute look up their item definitions by item type, as they would in the project instance.
The node then exports the values it produces.

Because pass 2 runs `ItemGroupIntrinsicTask` unchanged, compute item operations support `Remove`, metadata-only modifications, and the item modifiers.
Every step that reads or writes an item type imports its full value, so these follow the value flow described above.

### Differences from the Prototype

- The prototype stores compute property and item elements as raw text and fully expands them during compute; pass 1 now expands them as far as precompute can, as the prototype already does for task parameters.
- The prototype loses metadata when precompute items are written into compute text; temporaries keep it.
- The prototype transports values as JSON, losing escaping and item definitions; this design uses existing instance translation.
- The prototype splits conditions into `Condition` and `DeferredCondition`; this design uses a single `Condition` whose evaluation follows the element's phase.
- The prototype batches compute property and item operations during compute; this design does too, except for batching over both phases.

## Future: Expansion Plans

Pass 2 reparses text during compute.
A later version can replace the pass 1 text with expansion plans: structured, translatable results that compute finishes without reparsing.

### Key Ideas

- Expansion produces a result that is either a value or a plan, built up as the passes run.
  A normal build never defers anything, so its result is always a value.
- A plan is a small tree of translatable nodes: literals, references to compute-phase properties, item expressions over a compute-phase item type or a precompute snapshot, and concatenations.
- Precompute values are folded into literals, and literals are never rescanned, so escaping needs no special rules.
- Conditions are parsed into the normal condition tree and partially evaluated with normal short-circuit rules; the remaining tree is pruned, serialized, and evaluated during compute.
- Compute walks the plan and calls the normal item and condition code, without parsing.
- The deferral decision is a struct type parameter, so a normal build has no added checks.

### Challenges

- **Expansion and evaluation are coupled.**
  The expander passes hand strings to each other, condition parsing and evaluation run together, and intrinsic tasks expand and apply in one loop.
  These must be separated before results can be captured.
- **Risk to normal builds.**
  The changes touch hot, core parts of the expander, condition evaluator, and intrinsic tasks, and need benchmarking.
- **Duplicated code.**
  Building plan code beside the existing code avoids that risk but repeats its control flow, and the copies can drift.
