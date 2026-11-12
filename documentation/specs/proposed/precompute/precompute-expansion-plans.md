# Precompute Expansion Plans

## Status

Draft.

## Overview

Precompute mode runs a build in three steps:

```text
Evaluation -> Precompute -> Compute
```

Evaluation runs as MSBuild does today.
The precompute phase executes targets, runs safe work immediately, and encodes compute-phase work into the graph.
The compute phase executes graph nodes.

Project-scoped properties and items may be marked `Phase="Compute"`.
Their evaluation is delayed: precompute mode captures what evaluation would have done and stores it in the graph to run during compute.

```xml
<ItemGroup>
  <Gen Phase="Compute" Include="$(OutDir)*.cs" />
</ItemGroup>
```

Here `@(Gen)` is not known until compute, because the glob must observe files written by compute nodes.

## Problem Statement

Precompute must expand everything it can and store only what depends on compute.

The prototype partially expanded expressions, stored the remainder as text, and reparsed it during compute with a rebuilt `Lookup` and `Expander`.
This loses escaping and metadata through stringification, is expensive, and repeats parsing.

Precompute needs a structured representation, called an expansion plan, that compute finishes without parsing MSBuild text.

## Phase

`Phase` is an attribute on project property and item elements.
Values are `Precompute` (default) and `Compute`.

A compute value does not exist until compute runs, so precompute can never read it.
A precompute operation that reads a compute name is an error.
A compute operation may read precompute names.

In V1 each property name and item type has a single phase per project configuration:

- A write whose phase differs from the name's existing writes is an error.
- `Phase` on item definition metadata is an error.

Back-compat for existing `Phase` item metadata is handled by separate planned work.

## Evaluation

Non-precompute evaluation is unchanged.

In precompute mode, when evaluation reaches a compute operation it:

1. parses the operation;
2. folds everything known at that point;
3. records an expansion plan in source order with its location and defining project;
4. does not apply the operation.

The evaluated `ProjectInstance` therefore contains only precompute state.
It also carries the compute names and their plans, so precompute target execution can distinguish a compute name from an undefined one.

Folding follows retail binding:

- property references bind to the value visible at that point in the property pass;
- item operations see final property values and final item definitions;
- item definition metadata is folded into compute item plans.

Structural consumers must be known during precompute.
A compute name read by an import, `Choose`/`When`, item definition, `UsingTask`, target definition, target condition, or `DependsOnTargets` is an error.

## Folding

Phase applies to the whole property or item operation.

A precompute operation evaluates as it does today, including globs, filesystem functions, and environment reads.
Those reads are recorded in the graph as precompute-phase inputs.

A compute operation is captured whole.
Within it, anything that depends only on precompute values is folded:

- property and item references;
- item definition metadata;
- pure property functions with known arguments.

Globs, filesystem functions, and environment, time, random, and other nondeterministic functions remain in the plan and run during compute.
Glob roots and filesystem paths are recorded as inputs.
A compute-dependent path in a glob or filesystem function is an error.

Folding requires classifying property functions as pure or impure.

## Compute Evaluation Node

Each project configuration with at least one compute operation gets one compute evaluation node.

- The node has no tasks.
  It runs the project's compute plans in source order.
- Its outputs are the values of the project's compute names.
  Every reader imports the same value.
- It has no value inputs.
  Evaluation runs before any target, so its plans only read folded precompute values and earlier compute operations in the same node.
- Its inputs are the recorded glob roots and filesystem paths.

Nodes that read a compute name depend on this node.

A later refinement splits the node into one node per dependency group.
A group contains compute operations that write the same name or read each other's names.
Splitting avoids false dependencies between unrelated compute names once file inputs create graph dependencies.

## Consumers

During precompute target execution:

- a compute task parameter that reads a compute name stores an expansion plan and depends on the name's producers;
- a precompute task parameter, condition, or batching expression that reads a compute name is an error.

## Expansion Plans

### Plan Nodes

A plan is a tree of immutable nodes:

```csharp
internal abstract class ExpansionPlanNode : ITranslatable
{
    internal abstract ExpansionPlanNodeKind Kind { get; }
}

LiteralPlanNode           { string EscapedValue }
LiteralItemsPlanNode      { ProjectItemInstance[] Items }
PropertyReferencePlanNode { string Name }
ItemReferencePlanNode     { string ItemType; string? SeparatorEscaped }
MetadataReferencePlanNode { string? ItemType; string Name }
ConcatPlanNode            { ExpansionPlanNode[] Parts }
ListPlanNode              { ExpansionPlanNode[] Fragments }
TransformPlanNode         { ItemReferencePlanNode Source; ExpansionPlanNode Transform; string? SeparatorEscaped }
GlobPlanNode              { string PatternEscaped; string RootEscaped }
```

- `ConcatPlanNode` joins parts into one string, such as `$(GenTag)-x`.
- `ListPlanNode` holds `;`-separated fragments, such as `@(Compile);@(Gen)`.
  A string consumer joins the fragments with `;`.
  An item consumer concatenates them, splitting literal strings into items.
- Reference nodes name a compute property or item type.
  They resolve against values produced earlier in the same node or imported from a producer.

Plans preserve retail expansion order: metadata, then properties, then items.
A substituted value only participates in passes after its substitution.
For example, a property whose value is `@(X)` is item-expanded where it is used; escaped `%40(X)` stays literal.

### Compute Operations

Each captured property or item operation becomes one compute operation:

```csharp
ComputePropertyOperation
{
    string Name;
    ExpansionPlanNode Value;
    ElementLocation Location;
}

ComputeItemOperation
{
    string ItemType;
    ExpansionPlanNode Include;
    ExpansionPlanNode? Exclude;
    (string Name, ExpansionPlanNode Value)[] Metadata;
    ProjectItemDefinitionInstance? ItemDefinition;
    string DefiningFileEscaped;
    ElementLocation Location;
}
```

`ItemDefinition` holds the item definition folded at capture.
`DefiningFileEscaped` preserves defining-project metadata for items created during compute.

### Types

| Type | Existing | Serialized | Role |
| --- | --- | --- | --- |
| `ProjectPropertyInstance` | Yes | Yes | Exported and imported property values |
| `ProjectItemInstance` | Yes | Yes | Literal items in plans; exported and imported item values |
| `ProjectItemDefinitionInstance` | Yes | Yes | Folded item definitions |
| `PropertyDictionary`, `ItemDictionary` | Yes | Yes | Exported and imported value tables |
| `ElementLocation` | Yes | Yes | Source locations for compute-time errors |
| `ExpressionShredder.ItemExpressionCapture` | Yes | No | Parses item expressions during capture |
| `GenericExpressionNode` | Yes | No | Parses and folds conditions during capture |
| `FileMatcher` | Yes | No | Expands globs during compute |
| `ExpansionPlanNode` hierarchy | No | Yes | Plans |
| `ComputePropertyOperation`, `ComputeItemOperation` | No | Yes | Compute evaluation node steps |

Graph node types belong to the precompute graph object model.
This design defines the payload those nodes carry.

### Example

```xml
<PropertyGroup>
  <OutDir>bin\</OutDir>
  <GenTag Phase="Compute">gen-$(Configuration)</GenTag>
</PropertyGroup>

<ItemGroup>
  <Compile Include="a.cs" />
  <Gen Phase="Compute" Include="$(OutDir)gen\*.cs" Exclude="$(OutDir)gen\skip.cs">
    <Origin>generated</Origin>
  </Gen>
  <GenRefs Phase="Compute" Include="@(Gen->'%(Filename).ref')" />
</ItemGroup>

<Target Name="Build">
  <Csc Phase="Compute"
       Sources="@(Compile);@(Gen)"
       References="@(GenRefs)"
       Define="$(GenTag)"
       OutputAssembly="$(OutDir)app.dll" />
</Target>
```

#### Evaluation

Evaluation applies `OutDir` and `Compile` normally.
It captures the three compute operations, folds `$(OutDir)` and `$(Configuration)`, and does not apply them:

```text
ComputePropertyOperation GenTag
  Value: Literal("gen-Debug")

ComputeItemOperation Gen
  Include: Glob(pattern: "bin\gen\*.cs", root: "bin\gen\")
  Exclude: Literal("bin\gen\skip.cs")
  Metadata:
    Origin = Literal("generated")

ComputeItemOperation GenRefs
  Include: Transform
    Source: ItemReference(Gen)
    Transform: Concat[MetadataReference(Filename), Literal(".ref")]
```

`GenTag` is fully folded but remains a compute name because its phase is `Compute`.
`GenRefs` reads `Gen`, which is allowed because both are compute.

The evaluated `ProjectInstance` contains `OutDir` and `Compile`, plus the compute names `GenTag`, `Gen`, and `GenRefs` and their operations.

#### Validation During Evaluation

- Every write to `GenTag`, `Gen`, and `GenRefs` must be `Compute`.
- No precompute operation may read them.
- No import, `Choose`/`When`, item definition, `UsingTask`, or target definition may read them.
- The glob root must not depend on a compute value.
- The compute operations may only use V1-supported constructs.

#### Precompute Target Execution

`Csc` is a compute task, so its parameters become plans:

```text
Sources:        List[LiteralItems([a.cs]), ItemReference(Gen)]
References:     ItemReference(GenRefs)
Define:         PropertyReference(GenTag)
OutputAssembly: Literal("bin\app.dll")
Imports:        GenTag, Gen, GenRefs from the compute evaluation node
```

A precompute task, target condition, or batching expression that read `@(Gen)` would fail here with its source location.

#### Graph Creation

At the end of precompute:

- The project gets a compute evaluation node.
  - Steps: the three compute operations in source order.
  - Inputs: directory `bin\gen\`.
  - Outputs: `GenTag`, `Gen`, `GenRefs`.
- The `Build` target gets a compute node.
  - Steps: `Csc` with its parameter plans.
  - Imports: `GenTag`, `Gen`, `GenRefs`.
  - Dependency: the compute evaluation node.

Graph validation checks that every import has a producer.

#### Compute

The compute evaluation node:

1. sets `GenTag` to `gen-Debug`;
2. globs `bin\gen\*.cs` with `FileMatcher`, removes the exclude, and creates `ProjectItemInstance` values with `Origin`, the folded item definition, and the defining file;
3. transforms `@(Gen)` into `GenRefs` items;
4. exports `GenTag`, `Gen`, and `GenRefs` in a `PropertyDictionary` and `ItemDictionary`.

The `Build` node imports those values, resolves each parameter plan to a string or items, and passes them to normal task parameter binding.
Parameter validation and conversion happen during compute, when the task is invoked.

## Plan Execution

Compute executes plans with a small plan interpreter.
Retail `Expander` is not changed in V1.

- Capture uses existing parsing helpers: `ExpressionShredder`, property scanning, and the condition parser.
  Fully foldable parts use the normal `Expander`.
- The interpreter uses existing helpers for item functions, globbing, and property functions.
- Unsupported constructs produce a clear error during precompute.

Moving the interpreter's logic into `Expander` is a later optimization.

## Value Transport

Producers export values; consumers import only the names they reference.

- Properties are `ProjectPropertyInstance` and items are `ProjectItemInstance`, held in `PropertyDictionary` and `ItemDictionary`.
  Their existing translation preserves escaping, metadata, defining file, and item definitions.
- The compute evaluation node exports the final value of every compute name, including empty and undefined values.
  Targets run after evaluation, so readers only observe final evaluation values.
- A missing value is an error; produced empty values are valid.

A reference is a name plus the ordered producers visible at the read point.
The compute evaluation node is always the first producer.
Producers compose as:

| Producer operation | Exports | Composition |
| --- | --- | --- |
| Property assignment | Full value | Last producer wins |
| Item include | Added items | Concatenated in precompute order |
| Item remove or update | Full list | Reads earlier producers and replaces them |

In V1 the compute evaluation node is the only producer, because compute task `<Output />` is not yet supported.

## Serialization

Plans use `ITranslatable`.

- Plan nodes are a small class hierarchy with a `byte` kind tag and one factory switch, following `TaskParameter`.
- Literal items use `ProjectItemInstance` translation.
- References store names and producer node IDs.
- Each operation stores its `ElementLocation` for compute-time errors.
- The graph format version covers plan kinds; an unknown kind is an error.

V1 does not deduplicate strings or items.

## V1 Limitations

### Compute-dependent conditions

Every condition must be fully evaluated during precompute.
This includes conditions on compute operations and on compute tasks.
A condition that reads a compute name, or uses an impure function such as `Exists`, is an error.
For example, `<Gen Phase="Compute" Include="..." Condition="Exists('$(OutDir)x')" />` cannot be conditioned on a file generated during compute.
Supporting this requires serializable condition plans that keep retail short-circuit behavior.

### Deferred functions

A property or item function that cannot be folded during precompute is an error.
This covers impure functions with known arguments, such as `$([System.IO.File]::ReadAllText('$(OutDir)ver.txt'))`.
It also covers pure functions applied to compute values, such as `$(CompProp.TrimEnd('\'))` or `@(Gen->Distinct())`.
Globs are the only deferred evaluation supported in V1.
Supporting functions requires a function plan node and a way to invoke `Function<T>` and item functions with already-resolved arguments.

### Compute task outputs

Compute tasks cannot use `<Output />`, so they cannot produce property or item values.
Compute operations captured from evaluation are the only source of compute values.
Each compute name therefore has a single producer, and producer composition is not exercised.

### Compute item operations

Compute item operations support `Include` with `Exclude` and metadata.
`Remove` and `Update` on a compute item type are errors.
These operations depend on `LazyItemEvaluator` matching semantics, such as `MatchOnMetadata`, that the plan interpreter does not reproduce in V1.

### File inputs

Glob roots and filesystem paths read by compute operations are recorded as inputs of the compute evaluation node but do not create graph dependencies.
Nothing guarantees the node runs after the nodes that write those locations; correctness relies on the graph executing in a suitable order.
All readers still observe the same value.

## Invariants

- Non-precompute behavior and performance are unchanged.
- Evaluation in precompute mode differs only by not applying compute operations.
- Compute does not parse MSBuild text.
- Escaped values remain escaped until the existing caller would unescape them.
- Item order and metadata are preserved.
- Property and item names compare case-insensitively.
- Missing compute values are errors; produced empty values are valid.
- Symbol names must be known during precompute; `@($(CFoo))` is an error.
- Graph creation does not depend on executing any part of the graph.

## Future: Target-Scoped Compute Properties and Items

Target-scoped compute properties and items use the same capture, plans, and execution as project-scoped ones.
Precompute parses them once, folds what is known, and stores the same minimal operation trees.
Compute runs them with the same interpreter, without rebuilding `Lookup` or `Expander` and without parsing MSBuild text.

### Shared Plan Builder

The plan builder that captures compute operations is written against `IPropertyProvider<T>` and `IItemProvider<T>`, the same interfaces `Expander` uses.
Evaluation data implements them through `IEvaluatorData`, and target execution implements them through `Lookup`.
Each provider also exposes its compute names.

| | Project-scoped | Target-scoped |
| --- | --- | --- |
| Capture point | `Evaluator` property and item passes | `PropertyGroupIntrinsicTask` and `ItemGroupIntrinsicTask` during precompute target execution |
| Provider | Evaluation data | `Lookup` |
| Output | `ComputePropertyOperation`, `ComputeItemOperation` | Same types |
| Runs in | Compute evaluation node | The target's compute node, in order with its tasks |

### Example

```xml
<Target Name="Generate">
  <Gen Phase="Compute" Sources="@(Schema)">
    <Output TaskParameter="Generated" ItemName="GenOut" />
  </Gen>

  <ItemGroup>
    <GenFiles Phase="Compute" Include="@(GenOut->'%(FullPath)')" Kind="$(GenKind)" />
  </ItemGroup>
</Target>
```

During precompute, with `GenKind` set to `csharp`, `ItemGroupIntrinsicTask` captures the item operation against `Lookup`:

```text
ComputeItemOperation GenFiles
  Include: Transform
    Source: ItemReference(GenOut)
    Transform: MetadataReference(FullPath)
  Metadata:
    Kind = Literal("csharp")
```

The `Generate` compute node contains two steps: the `Gen` task, then the `GenFiles` operation.
`GenOut` resolves to the task output produced earlier in the same node.
`GenFiles` is exported, and later readers import it with `Generate` as a producer.

### Differences

Target-scoped operations add:

- value inputs: references resolve against earlier steps in the same node or against imports from earlier producers;
- composition: a target include appends to earlier producers, and a property assignment or item remove/update replaces them;
- intrinsic item-operation semantics such as `KeepMetadata`, `RemoveMetadata`, `KeepDuplicates`, and `Remove` with `MatchOnMetadata`;
- batching over `%(...)` buckets, which remains an error until compute-phase batching is supported;
- target-local `Lookup` scoping, which is resolved during capture.

The V1 limitations apply equally: compute-dependent conditions, deferred functions, and item remove/update are errors until the shared plan model supports them.

### Replacing the Prototype

The prototype stores deferred target elements as raw `ProjectPropertyGroupTaskPropertyInstance` and `ProjectItemGroupTaskItemInstance` text, then rebuilds `Lookup` and `Expander` during compute to reparse and rerun them.
The shared plan builder replaces that path, so project-scoped and target-scoped compute operations have the same representation, cost, and semantics.
