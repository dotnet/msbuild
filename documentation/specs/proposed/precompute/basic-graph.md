# Precompute Basic Graph

Precompute mode is composed of two logical phases: the precompute phase and the compute phase.
The precompute phase re-uses the existing evaluation and target execution logic of MSBuild and immediately executes work assigned to the precompute phase, and creates a graph of work assigned to the compute phase.
The compute phase executes the graph of work based on their dependencies without evaluating the originating projects.

This specification describes the first basic implementation of precompute mode in MSBuild.
The goal being to be a minimal implementation that is complete enough to execute simple project files.
Note that many of the restrictions or simplified scope in this document may be temporary so do not assume them to always be a permanent design choice.

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
  <MyTaskName Phase="Precompute" />
</Target>
```

`DefaultPhase` and `Phase` are MSBuild expressions evaluated during the precompute phase with ordinary expansion semantics.
An empty or missing task `Phase` falls back to `DefaultPhase`.
In precompute mode the resulting value must be nonempty and either `Precompute` or `Compute`, case-insensitively.
Requiring an explicit result avoids silently assigning an unannotated task to the wrong phase.

`Phase` is a reserved task attribute rather than a task parameter.
This intentionally changes compatibility for tasks that previously exposed a parameter named `Phase`.

### Precompute Phase
Precompute phase logic will only be enabled on internal data structures.

#### Graph Constructor
A single graph constructor exists for the entire build session and all build requests, including nested requests, will contribute to the same graph.
This will be constrained to a single in-process node (i.e. no multi-node or mult-threading support).
The node must be configured to enable precompute which will create this constructor.
Then all requests issued to that node must have precompute enabled and when building will have the graph constructor available.
Any time a node's precompute status does not match a request's precompute enablement, an error will be raised.
The node will then be shut down which will finalize the graph.

#### Evaluation
Evaluation of projects will occur unchanged from existing MSBuild.

#### Target Execution
Logic to walk targets will follow the same rules as existing MSBuild except that the up-to-date check will be always considered as false without looking at the state of the disk.
This makes the precompute mode always be a more deterministic specification of the full build since nodes cannot accidentally differ their behavior because of skips due to up-to-date checks.

The first change in behavior will be when executing a target's batch.
If a target has no batching then it is treated as a single batch.

The target's body will be processed in order.
Item and property groups are evaluated immediately.
Tasks will be processed based on their phase.
Parameters will be expanded and task batching performed unchanged from existing MSBuild regardless of the phase.
If the task is precompute phase, it will be executed.

Compute tasks must resolve to an `AssemblyTaskFactory` registration without loading the task assembly. Exact registrations use normal registration precedence. A partial task-name match is supported only when exactly one compatible registration exists; otherwise precompute fails with an ambiguity error. Custom and inline task factories are unsupported. Assembly loading, task-type validation, and parameter validation are deferred until compute execution.

If the task is compute phase, the information about the task will be captured without loading their factory or task assembly.
There must be an unambiguous single `AssemblyTaskFactory` registration selected through the ordinary task-name, override, runtime, and architecture rules failing otherwise.
Compute phase tasks cannot have an `<Output />` element.

If there is at least one compute task in the target's body, a compute node will be created.
The compute node's unique name will be the composed of the configuration (likely a hash of it), the target name, and the batch's index (if there are more than one batch).
The information about the configuration will be stored separately as to not duplicate it for each node.
Ordinary target `Inputs` and `Outputs` are expanded for each target batch, normalized, de-duplicated, then captured as the inputs and outputs of the compute node.
Each compute task's batch will be encoded as a step in the node.
This will include the information about how to load the task and the expanded parameters for the batch.
Notably, the parameters will be stored in their state just before the type conversion for the task since that conversion can only happen after the task is loaded.
A task with no batching is effectively a single batch task.

Any failures while processing the target will discard the node and it will not be included in the graph.

`MSBuild` and `CallTarget` intrinsic tasks are always treated as precompute tasks and cannot be designated as compute tasks.

#### Finalization
After all projects have been built in the precompute phase the graph is finalized.
If any two nodes may produce the same normalized output path a detailed error will be reported.
Any violations result in the precompute phase failing.

### Compute Phase: Single Node Execution
The ability to execute a single node will be implemented as an internal feature.

A node is executed by going through its steps.
This execution does not contain any sort of scheduling.
For example, it does not inspect timestamps of inputs/outputs or ensure prerequisite nodes are complete.

The executor will create the necessary task host and project context for executing the node's steps.
This will include very basic console-based logging and no binlog support.
It will also include the project context and working directory.
Environment variables will be whatever is given to the executor.
Concurrent execution is not supported.

To run a task the executor will reuse the components in MSBuild to load the task.
No expansion of the parameters will need to happen as the graph has the expanded values.
However, the code to validate the parameters and assign them to the task will be re-used.
Only rudamentary support for runtime/architecture/STA support for a task will be supported right now.
The task will be executed and if it fails or throws an exception it will be reported as a task failure.

## Considerations

- **Non-Precompute Compatability**: There will be no change in behavior when the new precompute mode is not enabled.
- **Precompute Compatability**: Precompute mode will keep behavior identical to non-precompute mode except when explicitly noted in the specification.
- **Documentation**: This specification will be turned into documentation as the functionality is being implemented.
- **Dependencies**: No new dependencies will be introduced by this work.
- **Code Sharing**: Code sharing between non-precompute and precompute should be maximized.
- **Unit Testing**: New unit tests will be added to validate the logic specific to the new precompute/compute phases

