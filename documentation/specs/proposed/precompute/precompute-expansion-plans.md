# Precompute Expansion Plans

## Status

Draft.

This specification describes how properties and items in targets are expanded when some of their values are only available during the compute phase.

## Feature Design

### Background

Compute work often produces properties and items that later work consumes:

- a compute task writes properties and items through `<Output />`;
- a property or item operation inside a target is assigned to the compute phase, for example to glob files that only exist during compute.

Properties and items in targets are parsed and expanded once, during precompute. 
When they run in the compute phase, the result is captured as an expansion plan: a minimal representation that compute expands efficiently once compute-phase properties and items are available.

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
`Phase` becomes a reserved attribute on item elements in all modes, so it can no longer be used to set metadata named `Phase`.

A property or item can start as precompute and later become compute-phase.
Its precompute value at that point carries into the compute phase:

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
Every precompute property and item operation runs immediately, so graph-shaping state, such as the `Compile` items that feed a target's `Inputs`, is fully known when the graph is built.

Compute tasks and compute operations are captured into the target's compute node instead of running.
Capturing parses and expands their expressions once, as far as precompute can.

### Expansion Plans

Inside compute work, precompute substitutes references to precompute values: precompute properties, and precompute items with their metadata.
Everything else is left for compute: references to compute-phase values and every operation, such as transforms, item functions, wildcards, and condition functions.
What remains is an expansion plan, stored in the graph.

| Expression in compute work | Expansion plan |
| --- | --- |
| `$(P)` | P's value |
| `@(P)` | P's items, with metadata |
| `@(C)` | a reference to C |
| `@(C);@(P)` | a reference to C, then P's items |
| `@(C->'$(P)\%(Filename)')` | a transform over C, with P's value in the template |
| `@(P->'%(Filename).obj')` | a transform over P's items, run during compute |
| `$(OutDir)gen\*.cs` | a wildcard, expanded during compute |

Here `P` is a precompute property or item and `C` is compute-phase.

Running operations during compute matches a normal build, because they see the same precompute values.
It also lets them see files that compute produces, without a list of which MSBuild functions are safe to run early.

Compute runs each node in a fresh MSBuild process, without the project or its state.
Capturing a plan, instead of saving the original text and parsing and expanding it again during compute, provides:

- **Efficiency.** Compute only fills in compute-phase values and runs the remaining operations.
- **Correct escaping.** Substituted values stay separate from syntax, so a literal `%24(Foo)` is never expanded as `$(Foo)`.
- **Self-containment.** A plan names exactly the compute-phase values it reads; everything else is already substituted.
- **Determinism.** A plan is a small, canonical description of the work, suitable as part of the node's identity.

### Conditions

Conditions on compute work may reference compute-phase values.
Precompute evaluates what it can, with the same short-circuit rules as a normal build:

- false AND unknown is false;
- true AND unknown keeps the unknown part;
- true OR unknown is true;
- false OR unknown keeps the unknown part.

A condition that is false during precompute drops its task or operation.
Any remaining condition is evaluated during compute.

### Compute Nodes and Values

Each target batch with compute work becomes a compute node.
Compute work is any compute task or compute property or item operation, so a target with compute operations but no compute task also becomes a node.
The node's steps are its compute tasks and operations in source order.

A node produces the compute-phase properties and items it writes, and consumes those it references.
It depends on the nodes that produce what it consumes.
Values flow between nodes in precompute order:

- a property's value is its latest producer's value;
- a node that reads an item type loads its full value and publishes the full value if it also writes it;
- a node that only adds to an item type does not load it, does not depend on its earlier producers, and publishes only the items it adds;
- an item's value is the latest full value followed by the items added after it, in order.

A missing value is an error; a produced empty value is valid.

### Compute Execution

The node executor imports the compute-phase values the node consumes.
For each step it evaluates the remaining condition, fills the plan's references with imported values, runs the remaining operations with the same expansion code as a normal build, including expanding items after properties, and then runs the task or applies the operation.
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
   `OutputDirectory` becomes `bin\gen\`, and `Sources` becomes the `Schema` items.
   Its `<Output />` makes `GenManifest` compute-phase.
3. `Gen` is a compute operation, so it is captured.
   Its wildcard is left for compute, because the generated files do not exist yet.
   `Gen` becomes compute-phase.
4. `GenRefs` transforms `Gen`, which is now compute-phase, so the transform is left for compute.
   `GenRefs` becomes compute-phase.
5. `FileWrites` is a precompute operation, so it runs immediately.
   If it referenced `@(Gen)`, it would be an error.

`Build` then captures `Csc`.
Its condition and parameters mix precompute values with references to `GenManifest`, `Gen`, and `GenRefs`.

#### Graph

- `Generate` node
  - Steps: `GenTask`, `Gen`, `GenRefs`.
  - Produces: `GenManifest`, `Gen`, `GenRefs`.
- `Build` node
  - Steps: `Csc`.
  - Consumes: `GenManifest`, `Gen`, `GenRefs`.
  - Depends on: `Generate`.

`GenDir` and `FileWrites` appear in neither node; their values are already substituted where they are used.

#### Compute

The `Generate` node runs `GenTask` and sets `GenManifest`.
It expands `bin\gen\*.cs`, applies the exclude, and creates `Gen` items with `Origin` and the item definitions for `Gen`.
It transforms `Gen` into `GenRefs` and exports all three values.

The `Build` node imports them, evaluates its condition, finishes each parameter, and runs `Csc`.

### Limitations

The following are errors in this version:

- **Property functions in compute work.**
  They cannot be represented in a plan yet; running them during compute requires changes to the property function implementation.
  Condition functions such as `Exists` are supported.
- **Batching over compute-phase items**, for targets, tasks, and operations.
  Batching over precompute items is resolved during precompute.
  A later version could support compute target batching with a container node that expands during compute into one child node per batch.
- **`Phase` on `PropertyGroup` and `ItemGroup`**, and a separate deferred condition attribute.
  A single `Condition` is partially evaluated instead.
- **Compute-phase values in target `Returns`.**
- **Compute-phase values in task attributes other than parameters and `Condition`**, such as `ContinueOnError`.

### Open Questions

#### Compute item operations beyond `Include`

Compute item operations support `Include`, `Exclude`, and metadata.
Whether, when, and how to support `Remove`, metadata-only modifications, and the `KeepMetadata`, `RemoveMetadata`, `KeepDuplicates`, and `MatchOnMetadata` modifiers is not yet decided; until then they are errors.

These operations read the type's full value, so they fit the value flow described above.
Supporting them requires compute to run the existing `ItemGroupIntrinsicTask` remove, modify, and duplicate-matching logic on specs resolved from plans.

### Considerations

- **Non-Precompute Compatibility**: Behavior and performance are unchanged when precompute mode is not enabled, except that `Phase` is reserved on item elements.
- **Precompute Compatibility**: Compute work produces the same results as a normal build; anything that cannot be represented is an error.
- **Code Sharing**: Precompute and compute use the same expansion code as a normal build; there is no second implementation.

## Implementation

### Implementation Options

Two ways of structuring the code are considered; the sections after this one describe Option 2, the intended plan.

#### Option 1: Substitute escaped values, reparse during compute

This would be similar to the prototype and purely meant to unblock other work by making minimal changes to how property expansion and item expressions work today.
Precompute substitutes every value it knows into the operation's text, escaped so that a reparse cannot find new syntax in it.
Compute reparses that text with the normal expander, evaluator, and intrinsic task code.
There are no plan types.

- **Precompute output**, per compute operation and bucket:
  - Bucket metadata and precompute property values, including property function results, are substituted as the normal passes produce them, with `$` written as `%24`, and `'` written as `%27` in condition text.
    A normal build never rescans a value for `$(`, so this changes nothing.
  - `@(` and `;` in values are left as is, because a normal build's item pass sees them too.
  - References to compute-phase properties and all item expressions stay as text, with snapshots of the precompute item types they name.
  - Property functions that read a compute-phase value are an error.
- **Conditions:** a condition that reads no compute-phase name after substitution is evaluated during precompute; otherwise it is evaluated whole during compute.
- **Compute:** builds a `Lookup` from the snapshots and imported values, then runs the existing intrinsic task logic on the substituted element with no batching.
  The final unescape restores the original characters, as in a normal build.
  The compute-phase names an operation reads are found by scanning its text.
- **Known limitation:** a substituted value containing a literal `%(` outside an item transform is expanded as metadata by the compute reparse, where a normal build leaves it literal.
  Inside a transform template, such as `@(Compile->'$(ObjTemplate)')`, it behaves as in a normal build.

Pros: very little new code; normal build semantics; most values collapse into the text.
Cons: compute reparses each operation; item expressions stay as references; the literal `%(` limitation above; conditions that read compute-phase values are not partially evaluated.

#### Option 2: One build-and-execute path (intended)

Every expression and condition is built by the existing parsers and passes into an `ExpansionResult` or a condition tree, then executed.
A normal build never defers anything, so its result is always a value or an ordinary tree.

- **Expander:**
  - The passes write into a shared struct result builder instead of handing strings to each other.
    Known text goes into the pooled string builder as today; deferred parts allocate only when one appears, so a normal build allocates nothing extra.
  - The deferral decision is a struct type parameter: one that never defers for a normal build, and one backed by compute-phase state for precompute.
    Each is compiled separately, so the normal build has no added checks.
  - Execution builds item expression captures from plan nodes and runs the normal item pass, which also covers item syntax in a carried precompute value.
  - Splitting and itemizing becomes one step shared by both; the existing entry points become wrappers that return the value.
- **Conditions:**
  - The condition plan is the normal `GenericExpressionNode` tree.
  - Each node gains partial evaluation, returning true, false, or a remaining node using its own short-circuit and comparison logic, and serialization.
  - Leaves hold source text and an optional plan; compute evaluates the remaining tree as a normal build would.
- **Intrinsic tasks:**
  - One loop produces a condition result and an `ExpansionResult` per operation, then applies it immediately or records a compute step.
  - The add path is split into stages that both a normal build and compute call: fragments to items, metadata, then adding to `Lookup`.

Pros: no duplicated logic; a normal build and precompute differ only in the deferral policy.
Cons: broader changes to the property and item expanders, the condition nodes, and `ItemGroupIntrinsicTask`; the items entry point is a hot path in normal builds and needs benchmarking.

### Compute-Phase State

`Lookup` records compute-phase property names and item types, and the precompute value each had when it became compute-phase.

`Lookup` also enforces precompute reference errors.
Reading a compute-phase property, or the items of a compute-phase item type, through `Lookup` is an error.
This covers every precompute reader, including those that bypass `Expander`, such as `BatchingEngine` and `ItemGroupIntrinsicTask`.
Only the plan entry points described below check whether a name is compute-phase before reading it.

The state exists only during precompute.
Outside precompute the check is a single null test, and evaluation does not use `Lookup`.

### Expander Entry Points

The existing expansion methods, such as `ExpandIntoStringLeaveEscaped` and `ExpandIntoItemsLeaveEscaped`, keep their signatures and behavior.

Compute work uses new entry points that return either a value or a plan:

```csharp
internal readonly struct ExpansionResult
{
    public string? Value { get; }            // fully expanded, escaped
    public ExpansionPlanNode? Plan { get; }
}

internal ExpansionResult ExpandOrPlanIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location);
internal ExpansionResult ExpandOrPlanIntoItemsLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location);
```

- The string entry point is used for property values, metadata values, and condition operands.
  It expands a plain `@(P)` to text, as string expansion does today.
- The items entry point is used for item `Include` and `Exclude` and for compute task parameters.
  It keeps every item expression in the plan, so precompute items keep their metadata.
  A task parameter's type is not known until the task is loaded during compute, so compute decides whether the parameter becomes items or a string.

The new entry points run the same metadata, property, and item passes once, and build the plan from what they find.
The existing entry points are unchanged.

The new entry points report property reads and writes to the existing tracking, such as `PropertiesUseTracker` and BuildCheck, as normal expansion does.
A reference to a compute-phase property is reported as a read where it is captured, even though its value is not read until compute, and a compute write is reported as a write where it is captured.

### Capturing Plans

Precompute parses each expression once and builds plan nodes directly:

- The metadata pass is unchanged; batching over compute-phase items is an error, so metadata is always known.
- The property pass substitutes precompute property values, so the item pass sees the same text as in a normal build.
  A reference to a compute-phase property becomes a `PropertyReference`.
  If the property's carried precompute value contains item syntax, the item types it references are recorded on the `PropertyReference`, as snapshots or references.
  Property functions in compute work are an error.
- The item pass parses item expressions with `ExpressionShredder` as it does today.
  Each item expression becomes an `ItemExpression` whose step arguments and separator are plans, so a compute-phase property inside a transform template, such as `@(Foo->'$(ComputeDir)%(Filename)')`, is a `PropertyReference` in the step.
  A plain `@(P)` in the string entry point is expanded to text.
- The remaining text becomes literals.

A compute-phase reference where a name is expected, as in `@($(CFoo))`, is an error.
No transform, item function, or property function runs on a compute-phase value during precompute.

### Consuming Plans

- A string consumer concatenates the plan's parts; item expressions produce their usual string form.
- An item consumer splits literal text and property values on `;`, as item expansion does after property expansion.
  A fragment that is exactly one item expression produces items with their metadata.
  Any other fragment produces an item spec; item `Include` and `Exclude` specs are expanded with `FileMatcher`.
  An item expression that shares a fragment with other text is an error.

### Condition Plans

`ConditionEvaluator` separates parsing a condition from evaluating it.
One entry point parses a condition into a `GenericExpressionNode` tree; another evaluates a tree with a given evaluation state.
The existing entry point keeps its parsed-tree cache and calls both.

Precompute parses each condition into the existing tree, captures each expandable operand as a plan, and folds subtrees whose operands are known, using the short-circuit rules above.
Function calls are never folded.
Operands are string plans, except function arguments, which are item plans of the argument after its path is fixed, as in a normal build.
A remaining condition becomes a `ConditionPlan`; each leaf keeps its source text and its plan.

During compute, the remaining condition is rebuilt as a `GenericExpressionNode` tree.
Each `StringExpressionNode` leaf keeps its source text and expands its plan only when the evaluator reads it, so short-circuited operands are never expanded.
Checks that depend on source text, such as the empty-operand shortcut and the `$(MSBuildToolsVersion)` version comparison, behave as in a normal build.
`ConditionEvaluator` then evaluates the tree with its normal semantics.

Outside precompute a leaf has no plan; each leaf expansion costs one null check and evaluation allocates nothing new.

### Plan Execution

Compute walks the plan nodes; it does not parse or rescan text:

- `PropertyReference` nodes are filled with imported escaped values;
- `ItemExpression` nodes become `ItemExpressionCapture` instances built from their fields, with step arguments and separators filled the same way, and run through the existing transform and item function code;
- an item consumer splits literal text and property values on `;` while walking the nodes, using the same rule as `SemiColonTokenizer`, and item specs are expanded with the existing item-spec and `FileMatcher` logic.

The one exception is a carried precompute value with item syntax: after the value is filled in, its item syntax is expanded with the items recorded on the `PropertyReference`, as the item pass would in a normal build.
Values produced during compute contain no item syntax.

### Serialized Types

The node payload adds these types, all implementing `ITranslatable`:

```csharp
LiteralPlanNode           { string EscapedValue }
PropertyReferencePlanNode { string Name; ItemExpressionPlanNode[]? ItemInputs }
ItemExpressionPlanNode    { string ItemType; ProjectItemInstance[]? Items; ItemExpressionStep[] Steps; ExpansionPlanNode? Separator }
ConcatPlanNode            { ExpansionPlanNode[] Parts }

ItemExpressionStep        { string? FunctionName; ExpansionPlanNode? Arguments }   // null FunctionName = transform

ConditionPlan             { ConditionPlanKind Kind; ConditionPlan[] Children; string? Text; ExpansionPlanNode? Operand }   // Text: leaf source text or function name

ComputePropertyOperation  { string Name; ExpansionPlanNode Value; ConditionPlan? Condition; ElementLocation Location }
ComputeItemOperation      { string ItemType; ExpansionPlanNode Include; ExpansionPlanNode? Exclude; ComputeMetadata[] Metadata;
                            ConditionPlan? Condition; ElementLocation Location }
ComputeMetadata           { string Name; ExpansionPlanNode Value; ConditionPlan? Condition }
```

An `ItemExpressionPlanNode` always records its item type; it reads either that compute-phase item type or, when `Items` is set, a snapshot of precompute items.
`ItemInputs` holds the items for item syntax in a property's carried precompute value.
Step arguments and the separator are argument text, such as a transform template, with compute-phase properties as references.

| Type | New or reused | Use |
| --- | --- | --- |
| Plan nodes, `ItemExpressionStep` | New | Expansion plans; rebuilt as `ItemExpressionCapture` during compute |
| `ConditionPlan` | New | Remaining conditions; rebuilt as `GenericExpressionNode` during compute |
| `ComputePropertyOperation`, `ComputeItemOperation`, `ComputeMetadata` | New | Node steps for compute operations |
| `ProjectItemInstance` | Reused | Precompute item snapshots; exported and imported items |
| `ProjectPropertyInstance` | Reused | Exported and imported properties |
| `ProjectItemDefinitionInstance` | Reused | Node table of item definitions for items created during compute |
| `PropertyDictionary`, `ItemDictionary` | Reused | Export and import tables |
| `ElementLocation` | Reused | Source locations for compute-time errors |

Compute task steps hold parameter plans and a condition plan.

Each node also holds the project's defining file and an item definition table for every item type its operations and task outputs create.
Items created during compute look up their definition by item type, as they would in the project instance.

Plan nodes use a `byte` kind tag and one factory switch, following `TaskParameter`.
The graph format version covers plan kinds; an unknown kind is an error.
This version does not deduplicate strings or items.

### Differences from the Prototype

- The prototype stores compute property and item elements as raw text and reruns them during compute against a rebuilt `Lookup`; this design stores plans.
- The prototype substitutes compute-phase values into task parameters as `$(Name)` and `@(Type)` text and re-expands it during compute; this design stores references.
- The prototype transports values as JSON, losing escaping and item definitions; this design uses existing instance translation.
- The prototype splits conditions into `Condition` and `DeferredCondition`; this design partially evaluates a single `Condition`.
- The prototype batches compute property and item operations during compute; this design makes it an error.

