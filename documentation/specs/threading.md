# Threading in MSBuild Worker Nodes

MSBuild can build multiple projects in parallel with the `-m` switch. This launches many worker processes (**nodes**) and assigns work to them. Each is generally dedicated to building a single project at a time.

Projects can have dependencies on other projects. This is often represented with `@(ProjectReference)` items, but can be more complex--a dependency is created when a task calls the `IBuildEngine.BuildProjectFile` method or an equivalent. When this happens, the project is _blocked_: it cannot progress until the request it generated is built.

While a project is blocked, the node that was building it can do work on other projects. The build engine will save project-specific state like environment variables and current working directory and then do something else (if the scheduler has work to assign to it).

This is logically single-threaded, via a continuation model: a node is building a single chain of things, which may be suspended and continued after building another project in the same thread. The thread used to do this work is named `RequestBuilder thread`. It is not the main thread of the process because some other work goes on in parallel, like inter-process communication.

It is sometimes useful to be able to do computational work within one project while *also* freeing up the node to do work on another project. When this is desired, a task can call `IBuildEngine3.Yield()` to indicate that the node can do other work until it calls `IBuildEngine3.Reacquire()`.

When a project is _yielded_, the node is also yielded. The scheduler may then decide to assign additional work to the node. If it does so, the node will start a new RequestBuilder thread to do the new work, because the original thread will still be running the task code between `Yield()` and `Reacquire()`.

The scheduler limits the amount of total work being done, including both executing and yielded nodes, to attempt to avoid starting too much parallel work and bogging down the operating system. As a result, it's rare for a single node to have more than two or three RequestBuilder threads, though there is no hard bound on the number of threads in a single node.

If multiple RequestBuilder threads have been started and are idle in a single worker node, any of them may be used when a request is assigned to that node (or unblocked).

## Yielding from a TaskHost

Starting with change wave 18.14, TaskHosts with protocol version 9 support `IBuildEngine3.Yield()` and `Reacquire()` callbacks.
Older peers retain the previous no-op behavior.
Set `MSBUILDDISABLEFEATURESFROMVERSION=18.14` to restore that behavior.
The parent sends its yield policy before each task configuration, so reused hosts honor the parent's change wave.

The parent forwards these callbacks on the task's execution thread.
`Reacquire()` returns only after the scheduler grants the node, and releases requested cores through the normal task-engine contract.
The task must call `Reacquire()` on the thread that called `Yield()`.
Repeated yields and reacquisition without a preceding yield are invalid, as with in-process tasks.
If a task completes or disconnects while yielded, the parent reacquires before processing its result or releasing its handler.

A yielded task still occupies its host process.
The provider allocates another matching host slot when other work needs that host kind, then reuses idle slots.
Yield-capable hosts also use separate processes for nested build callbacks.
Otherwise, an outer callback could resume in the same process while its inner task remains yielded.
Legacy peers keep the existing callback handler stacks and nested-task behavior.

Explicit yield does not change the host's active-task count or exchange its process environment.
Each overlapping task therefore retains its own environment and current directory, including while external tools run.
This isolation requires additional host processes and memory.
The scheduler still controls node and core allocation.
