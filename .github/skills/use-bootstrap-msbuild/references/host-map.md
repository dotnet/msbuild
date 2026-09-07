# Identify the host before investigating

"TaskHost" can mean an object, a modern MSBuild child process, or a legacy executable. Resolve that ambiguity before building a project or tracing a transport.

## Process roles

The mappings come from [NodeMode](../../../../src/Framework/NodeMode.cs) and dispatch in [XMake](../../../../src/MSBuild/XMake.cs). Recheck them at the revision being investigated.

| Evidence | Role | Starting point |
|---|---|---|
| MSBuild launched with `/nodemode:1` | Out-of-process build worker | Worker-node execution and logging |
| MSBuild launched with `/nodemode:2` | Modern out-of-process TaskHost | [MSBuild/OutOfProcTaskHostNode.cs](../../../../src/MSBuild/OutOfProcTaskHostNode.cs) |
| MSBuild launched with `/nodemode:3` | RAR service node | NodeMode/XMake dispatch |
| MSBuild launched with `/nodemode:8` | MSBuild server | Server lifecycle and request forwarding |
| `MSBuildTaskHost.exe` from its separate project | Legacy .NET Framework compatibility host | [MSBuildTaskHost README](../../../../src/MSBuildTaskHost/README.md) and its stricter local instructions |

Do not choose `MSBuildTaskHost.csproj` merely because the symptom mentions a TaskHost. For ordinary MT task ejection, first inspect the modern MSBuild `/nodemode:2` path.

The [task-host diagnostic resource](../../../../src/Build/Resources/Strings.resx) includes "ran in TaskHost process" with task/caller process IDs, new-host, sidecar, and node-reuse details. Search the relevant diagnostic/binlog output when available.

A TaskHost process alone does not establish **why** the task ran there. Trace task compatibility/attestation, `UsingTask` factory/runtime/architecture settings, and launch evidence. Absence of a message is not proof that a different path ran.

## Objects are not processes

[TaskHost](../../../../src/Build/BackEnd/Components/RequestBuilder/TaskHost.cs) implements the engine interface exposed to tasks. [TaskExecutionHost](../../../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) manages task execution. Neither class name by itself identifies which OS process executed a task.

For a reproducible claim, record the binary path, mode, process identity, and relevant code/transport boundary together. Keep baseline and candidate outputs isolated.
