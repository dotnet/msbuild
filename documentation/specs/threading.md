# Threading in MSBuild Worker Nodes

MSBuild can build multiple projects in parallel with the `-m` switch. This launches many worker processes (**nodes**) and assigns work to them. Each is generally dedicated to building a single project at a time.

Projects can have dependencies on other projects. This is often represented with `@(ProjectReference)` items, but can be more complex--a dependency is created when a task calls the `IBuildEngine.BuildProjectFile` method or an equivalent. When this happens, the project is _blocked_: it cannot progress until the request it generated is built.

While a project is blocked, the node that was building it can do work on other projects. The build engine will save project-specific state like environment variables and current working directory and then do something else (if the scheduler has work to assign to it).

This is logically single-threaded, via a continuation model: a node is building a single chain of things, which may be suspended and continued after building another project in the same thread. The thread used to do this work is named `RequestBuilder thread`. It is not the main thread of the process because some other work goes on in parallel, like inter-process communication.

It is sometimes useful to be able to do computational work within one project while *also* freeing up the node to do work on another project. When this is desired, a task can call `IBuildEngine3.Yield()` to indicate that the node can do other work until it calls `IBuildEngine3.Reacquire()`.

When a project is _yielded_, the node is also yielded. The scheduler may then decide to assign additional work to the node. If it does so, the node will start a new RequestBuilder thread to do the new work, because the original thread will still be running the task code between `Yield()` and `Reacquire()`.

The scheduler limits the amount of total work being done, including both executing and yielded nodes, to attempt to avoid starting too much parallel work and bogging down the operating system. As a result, it's rare for a single node to have more than two or three RequestBuilder threads, though there is no hard bound on the number of threads in a single node.

If multiple RequestBuilder threads have been started and are idle in a single worker node, any of them may be used when a request is assigned to that node (or unblocked).
