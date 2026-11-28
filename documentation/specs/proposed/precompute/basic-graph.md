# Precompute Basic Graph

Precompute mode is composed of two logical phases: the precompute phase and the compute phase.
The precompute phase re-uses the existing evaluation and target execution logic of MSBuild and immediately executes work assigned to the precompute phase, and creates a graph of work assigned to the compute phase.
The compute phase executes the graph of work based on its dependencies without evaluating the originating projects.

This specification describes the first basic implementation of precompute mode in MSBuild.
The goal being to be a minimal implementation that is complete enough to execute simple project files.
Note that many of the restrictions or simplified scope in the initial implementation may be temporary so do not assume them to always be a permanent design choice.

## Feature Design
This specification introduces the first version of the precompute phase that will create a single graph from the project(s) being built.
Only tasks will be assignable to the compute phase in this version and all items and properties will be processed during the precompute phase.
Logic will be added to execute a compute node (i.e. from the build graph created by the precompute phase) but will not include any sort of scheduler of those nodes as of this time.

### Task Phase Specification

A `UsingTask` may define a default phase:

```xml
<UsingTask TaskName="MyTaskName" AssemblyFile="MyTaskNameAssembly.dll" DefaultPhase="Compute" />
```

An individual task usage may override that default:

```xml
<Target Name="Generate">
  <MyTaskName MSBuildPhase="Precompute" />
</Target>
```

`DefaultPhase` and `MSBuildPhase` are MSBuild expressions evaluated during the precompute phase with ordinary expansion semantics.
An empty or missing `MSBuildPhase` on a task invocation falls back to `DefaultPhase` from the `UsingTask`.
In precompute mode the resulting value must be nonempty and either `Precompute` or `Compute`, case-insensitively.
Requiring an explicit result avoids silently assigning an unannotated task to the wrong phase.

`MSBuildPhase` becomes a reserved task attribute rather than a task parameter.
A ChangeWave will be used to revert this behavior in the unlikely event that it causes compatibility issues with existing tasks.
This intentionally changes compatibility for tasks that previously exposed a parameter named `MSBuildPhase`.

### Precompute Phase
Precompute phase logic will only be enabled on internal code and data structures.
There will be no CLI or public API changes.

All messages specific to precompute will use the MSBPCxxxx message code range.

#### Graph Builder
A single graph builder exists for the entire build session and all build requests, including nested requests, will contribute to the same graph.
This will be constrained to a single in-process worker node (i.e. no multi-node or mult-threading support).
The worker node must be configured to enable precompute which will create this builder.
Then all requests issued to that worker node must have precompute enabled and when building will have the graph builder available.
Any time a worker node's precompute status does not match a request's precompute enablement, an error will be raised.
The worker node will then be shut down which will finalize the graph during `EndBuild()`.

#### Evaluation
Evaluation of projects will occur unchanged from existing MSBuild.

#### Target Execution
Logic to walk targets will follow the same rules as existing MSBuild except that the check of input/output timestamps will always consider the outputs as missing and thus never cause a target to be skipped due to being up-to-date.
`SkipNoInputs` and `SkipNoOutputs` behavior is unchanged.
This makes the precompute mode always be a more deterministic specification of the full build since nodes cannot accidentally differ their behavior because of skips due to up-to-date checks.

Targets will follow the existing MSBuild behavior as well, except when a task is annotated with the compute phase as described below.
Each target will be split into one or more [batches](https://learn.microsoft.com/visualstudio/msbuild/msbuild-batching).

Each batch will turn into one compute node in the build graph when a one or more tasks in its body have phase compute during processing.
A compute node is a list of task invocation(s) that can be executed to produce a set of outputs from a set of inputs.
Each task invocation will have information about how to load the task and the values to set for the parameters.
The `Inputs` and `Outputs` determined for that batch are normalized, de-duplicated and stored in the compute node if it is created.

Because one batch may turn into one compute node, a compute node is unique per configuration (i.e. the combination of project file path, initial global properties, and tools version), target name, and the batch's index.
The information about the configuration will be stored in the graph separately to avoid duplication across compute nodes and is used to ensure we can execute the tasks with the appropriate context.

Each target batch will be processed as follows.
Item and property groups are evaluated immediately.
Tasks will have different behavior based on their phase.
Parameters will be expanded and task batching performed unchanged from existing MSBuild regardless of the phase.
Expansion will disallow any variables that are not constant over the entire target batch such as `$(MSBuildLastTaskResult)`.
If the task is annotated to run in the precompute phase, it is executed immediately with ordinary MSBuild behavior, including its `<Output />` elements.

If the task is marked to run in the compute phase, first the task must resolve to an `AssemblyTaskFactory` registration without loading the task assembly.
Exact registrations use normal registration precedence.
A partial task-name match is supported only when exactly one compatible registration exists; otherwise precompute fails with an ambiguity error.
Custom and inline task factories are unsupported.
This supports the most common scenario where a task is registered with an assembly and can be resolved without ambiguity.

Assembly loading, task-type validation, and parameter validation will be done when the compute node is executed.

Compute phase tasks cannot have an `<Output />`, `<ContinueOnError />`, or `<OnError />` elements and will fail with an error if present.

Task batching will make a task invocation in the compute node per batch.
Captured parameter values, for each task batch, preserve their expanded scalar or item-list representation (including metadata).
The load information and the parameters become a task invocation in the compute node.

Any failures while processing the target will discard the node and it will not be included in the graph.
The failure will still fail the build request and overall build like MSBuild normally would.

`MSBuild` and `CallTarget` intrinsic tasks are always treated as precompute tasks and cannot be designated as compute tasks.

#### Finalization
After all projects have been built in the precompute phase the graph is finalized.
This is done during the EndBuild API similar to where the results cache is written.
If any two nodes may produce the same normalized output path a detailed error will be reported and the overall build will fail.
Any violations result in the precompute phase failing.

### Compute Phase: Single Node Execution
The ability to execute a single compute node will be implemented as an internal API (no CLI or public API).

A node is executed by going through its steps.
This execution does not contain any sort of scheduling.
For example, it does not inspect timestamps of inputs/outputs, cleanup outputs, or ensure prerequisite nodes are complete.
To execute everything in the build graph, this execution would need to be done on the nodes in topological order according to their inputs/outputs.

The executor will create the necessary task host and project context for executing the node's steps.
This will include very basic console-based logging and no binlog support.
It will also include the project context and working directory.
Environment variables will be whatever is given to the executor.
Concurrent execution is not supported.

The executor will reuse existing MSBuild mechanisms to load and execute the task.
No expansion of the parameters will need to happen as the graph has the expanded values.
However, the code to validate the parameters and assign them to the task will be re-used.
Only rudimentary support for runtime/architecture/STA support for a task will be supported right now.
The task will be executed and if it fails or throws an exception it will be reported as a task failure.

## Considerations

- **Non-Precompute Compatibility**: There will be no change in behavior when the new precompute mode is not enabled.
- **Precompute Compatibility**: Precompute mode will keep behavior identical to non-precompute mode except when explicitly noted in the specification.
- **Documentation**: This specification will be turned into documentation as the functionality is implemented.
- **Dependencies**: No new dependencies will be introduced by this work.
- **Code Sharing**: Code sharing between non-precompute and precompute should be maximized. Any risky refactorings to create sharing opportunities should be behind a changewave.
- **Unit Testing**: New unit tests will be added to existing unit test assemblies to validate the logic specific to the new precompute/compute phase logic
- **End-To-End Testing**: There will no end-to-end tests since precompute mode is not accessible through the CLI or public API.
