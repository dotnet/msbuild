# Multi-request Target Crosstalk

*Arrival-order-dependent execution in a shared project instance*

## Summary

MSBuild may begin executing a project before all target requests for that project are known. Multiple requests for the same build request configuration share mutable project state and configuration-scoped target results. Consequently, separately arriving target requests are not necessarily composable: their combined behavior can depend on which request arrives first.

We call this **multi-request target crosstalk**.

## Relevant engine concepts

A `BuildRequest` identifies targets to execute and references a configuration by `ConfigurationId`.

A `BuildRequestConfiguration` represents the project path, tools version, and global properties used to build the project. Equivalent requests resolve to the same configuration. The configuration owns the loaded `ProjectInstance` and the `BaseLookup` containing the project's current properties and items.

Separately, `ResultsCache` retains an aggregate `BuildResult` for each `ConfigurationId`. Once a target has completed, later requests for that configuration reuse its result rather than executing it again.

Conceptually, requests for the same configuration therefore share:

1. The mutable state of the project instance.
2. The accumulated history and results of completed targets.

The remainder of this document uses **project instance** for the shared execution context and **configuration** when configuration identity or target-result caching is specifically relevant.

## Definition

**Multi-request target crosstalk** occurs when multiple target requests for the same configuration interact through shared project state or completed target results, causing behavior to depend on request arrival order.

The requests do not need to execute simultaneously. Crosstalk can occur whenever one request begins before the complete set of requests for that configuration is known.

## Mutable-state example

Suppose a project contains an auxiliary target that modifies an item consumed by `Build`:

````xml
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

- One requests `Build`.
- Another requests `Auxiliary;Build`.

If `Build` arrives first, `Compile` runs without `Additional.cs`. The later request runs `Auxiliary`, but `Build` and `Compile` already have results and are not executed again.

If `Auxiliary;Build` arrives first, `Auxiliary` adds `Additional.cs` before `Compile` runs. The plain `Build` request then reuses those target results.

The project files and requested targets are identical in both cases, but the compiled inputs depend on which request reaches the project first.

In practice, auxiliary targets often are not intended to affect `Build`. Nevertheless, they can accidentally modify properties or items that build targets consume.

## Target-order example

Crosstalk can also change effective target ordering without directly overwriting state.

Suppose an auxiliary target depends on a target that is also part of `Build`:

```xml
<Target Name="Auxiliary"
        DependsOnTargets="ResolveReferences" />

<Target Name="Build"
        DependsOnTargets="Prepare;ResolveReferences;Compile" />
```

If `Auxiliary` runs first, it pulls `ResolveReferences` forward. When `Build` later reaches `ResolveReferences`, MSBuild reuses its existing result.

The effective sequence is therefore different from the sequence produced by a standalone `Build` request. If the project relies on incidental ordering not fully expressed through target dependencies, the two sequences can produce different state or outputs.

## Why arrival order varies

Which request reaches a project first can change because of:

- Parallel scheduling and ordinary timing variation.
- Solution or traversal edges competing with project-reference edges.
- Incremental builds changing which projects or targets perform work.
- Differences between clean, incremental, and partially up-to-date builds.

This can make the behavior intermittent even when the project graph and inputs appear unchanged.

## Why the behavior is surprising

Each contributing behavior is independently established:

- Targets can mutate project properties and items during execution.
- Completed targets are not executed again for later requests to the same configuration.
- Requests can arrive after execution of the configuration has already begun.

Multi-request target crosstalk is the unexpected composition of those behaviors. It is a consequence of current engine semantics, but it is often difficult for project authors and engine contributors to predict or diagnose.
````