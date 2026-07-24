# Multi-Request Target Crosstalk

*Arrival-order*–*dependent execution in a shared project instance*

## Summary

MSBuild begins executing a project as soon as it sees one request for it. All requests for the same build request configuration share mutable project state and configuration-scoped target results. Separately arriving requests are therefore not necessarily composable: behavior can depend on which arrives first.

We call this **multi-request target crosstalk**.

## Relevant engine concepts

A `BuildRequest` references a project (plus global properties) and targets.

A `BuildRequestConfiguration` represents the project path, tools version, and global properties. Equivalent requests resolve to the same configuration, which owns the loaded `ProjectInstance` and the current properties and items.

Separately, `ResultsCache` keeps an aggregate `BuildResult` for each `ConfigurationId`. Later requests reuse target results instead of executing the targets again.

Requests for the same configuration therefore share:

1. The mutable state of the project instance.
2. The accumulated history and results of completed targets.

Here, **project instance** means the shared execution context; **configuration** is used when identity or target-result caching matters.

## Definition

**Multi-request target crosstalk** occurs when requests for different targets in the same configuration interact through shared project state or completed target results, making behavior depend on arrival order.

The requests do not need to execute concurrently. Because there is no aggregation of target lists, crosstalk will occur whenever multiple target lists are specified for a configuration.

## Mutable-state example

Suppose a project contains an auxiliary target that modifies an item consumed by `Build`:

```xml
<ItemGroup>
  <Compile Include="Normal.cs" />
</ItemGroup>

<Target Name="Auxiliary">
  <ItemGroup>
    <Compile Include="Additional.cs" />
  </ItemGroup>
</Target>

<Target Name="Build" DependsOnTargets="Compile" />

<Target Name="Compile">
  <Message Text="Compiling @(Compile)" Importance="High" />
</Target>
```

Two callers request different target sets:

- `Build`.
- `Auxiliary;Build`

If `Build` arrives first, `Compile` omits `Additional.cs`. The later request runs `Auxiliary` but reuses the existing `Compile` and `Build` results.

If `Auxiliary;Build` arrives first, `Auxiliary` adds `Additional.cs` before `Compile` runs. The plain `Build` request then reuses those target results.

The project files and requested targets are identical, but the compiled inputs depend on which request is scheduled first.

Auxiliary targets often are not intended to affect `Build`, but can accidentally modify properties or items it consumes.

## Target-order example

Crosstalk can also change target execution order, and thus build outputs, without directly overwriting state.

Suppose an auxiliary target depends on a target also depended on by `Build`:

```xml
<Target Name="Auxiliary"
        DependsOnTargets="ResolveReferences" />

<Target Name="Build"
        DependsOnTargets="Prepare;ResolveReferences;Compile" />
```

If `Auxiliary` runs first, it pulls `ResolveReferences` forward; `Build` later reuses that result.

The order differs from a standalone `Build`; here, `Prepare` doesn’t run before `ResolveReferences`.

## Why arrival order varies

Request arrival order can change because of:

- Parallel scheduling and ordinary timing variation.
- Solution or traversal edges competing with project-reference edges.
- Incremental builds changing which projects or targets perform work.

This can make the ordering inconsistent even when the projects are unchanged.

## Relationship to static graph builds

[Static graph builds](specs/static-graph.md#inferring-which-targets-to-run-for-a-project-within-the-graph) address one source of multi-request target crosstalk. Before execution, the graph propagates `ProjectReferenceTargets` mappings, aggregates each node’s target lists, and submits one request with the combined list. With a complete graph and target mappings, separately arriving project-reference requests cannot determine which targets start the project instance.

This eliminates request-arrival crosstalk for targets represented by the graph, but not in every graph build:

- A non-isolated graph build can encounter an `MSBuild` task invocation or target list that was not predicted by the graph and fall back to just-in-time execution, allowing crosstalk. An isolated graph build instead reports the missing result as an error.
- Aggregation removes arrival-order dependence, but the combined target list remains ordered. Static graph preserves order within each propagated list and concatenates lists from different incoming edges. Targets still share mutable state and execute once, so correctness can depend on ordering that is not clearly declared.

A complete static graph therefore prevents this crosstalk but does not make arbitrary target sets freely composable.

## Why the behavior is surprising

Each contributing behavior is established:

- Targets can mutate project properties and items during execution.
- Targets are guaranteed to execute at most once per build for a given configuration.
- Requests can arrive after the configuration starts executing.

The _composition_ is often difficult for project authors and engine contributors to predict or diagnose.
