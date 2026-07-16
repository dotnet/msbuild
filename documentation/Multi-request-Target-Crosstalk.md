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

Suppose an auxiliary target depends on a target that is also part of `Build`:

```xml
<Target Name="Auxiliary"
        DependsOnTargets="ResolveReferences" />

<Target Name="Build"
        DependsOnTargets="Prepare;ResolveReferences;Compile" />
```

If `Auxiliary` runs first, it pulls `ResolveReferences` forward. When `Build` later reaches `ResolveReferences`, MSBuild reuses its existing result.

The effective sequence is therefore different from the sequence produced by a standalone `Build` request. In this simplified case, that means that `Prepare` doesn’t run before `ResolveReferences`.

## Why arrival order varies

Which request reaches a project first can change because of:

- Parallel scheduling and ordinary timing variation.
- Solution or traversal edges competing with project-reference edges.
- Incremental builds changing which projects or targets perform work.

This can make the ordering inconsistent even when the project graph and inputs are unchanged.

## Relationship to static graph builds

[Static graph builds](specs/static-graph.md#inferring-which-targets-to-run-for-a-project-within-the-graph) address an important source of multi-request target crosstalk. Before execution, the graph propagates the `ProjectReferenceTargets` mappings, aggregates the target lists that reach each graph node, and submits one build request for that node with the combined target list. When the graph and its target mappings are complete, separately arriving project-reference requests therefore cannot determine which subset of targets starts the project instance.

This fully addresses the request-arrival aspect of crosstalk for target requests represented by the graph, but it does not eliminate the broader problem in every graph build:

- A non-isolated graph build can encounter an `MSBuild` task invocation or target list that was not predicted by the graph and fall back to classic just-in-time execution. That late request can still produce crosstalk. An isolated graph build instead reports the missing result as an error.
- Aggregation removes arrival-order dependence between the represented requests, but the aggregate target list still has an order. Static graph preserves order within each propagated target list, while target lists from different incoming edges are concatenated. Targets still share mutable project state and execute only once, so correctness can remain sensitive to ordering that is not encoded in target dependencies.

Static graph therefore prevents this form of crosstalk when its model is complete, but it does not make arbitrary target sets freely composable.

## Why the behavior is surprising

Each contributing behavior is independently established:

- Targets can mutate project properties and items during execution.
- Targets are guaranteed to execute exactly once per build per project instance/configuration.
- Requests can arrive after execution of the configuration has already begun.

Multi-request target crosstalk is the unexpected composition of those behaviors. It is a consequence of current engine semantics, but it is often difficult for project authors and engine contributors to predict or diagnose.
