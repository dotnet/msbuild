# Resolve Assembly Reference as Service Design

This document describes Resolve Assembly Reference as a Service

# Background

[MSBuild](https://docs.microsoft.com/visualstudio/msbuild/msbuild?view=vs-2019) is a universal build engine, used for building pretty much everything in the Microsoft world. It is available on command line (msbuild, [dotnet build](https://docs.microsoft.com/dotnet/core/tools/dotnet-build)), runs under the covers when building projects and solutions in Visual Studio, and is used as the local build engine in "higher-order" distributed build systems. Essentially all .NET applications use MSBuild as their primary build engine.

RAR is the acronym behind ResolveAssemblyReference (an MSBuild task) and ResolveAssemblyReferences (an MSBuild target). RAR is used in all .NET builds. Quoting the official documentation, RAR "_Determines all assemblies that depend on the specified assemblies, including second and nth-order dependencies._"

The RAR task has become very complex and slow over the years. It tends to rank high on the list of MSBuild's performance bottlenecks. There is an inherent cost to walking the assembly reference graph and computing the dependency closure, especially in terms of I/O operations. To address this, the task internally maintains caches, both in-memory and on disk. While it alleviates the problem somewhat, it is still a suboptimal solution because

1. The task runs in build nodes which are generally created as separate processes, one per logical CPU, so the in-memory state is duplicated.
2. Build nodes have limited lifetime and the in-memory state is lost when they die.
3. No state exists when the task runs for the given project for the first time.

    *NOTE:* This is tracked by issue [#5247](https://github.com/dotnet/msbuild/issues/5247).

There was already an attempt to introduce RAR as a service to MSBuild ([#3914](https://github.com/dotnet/msbuild/pull/3914)). This PR was not completed mainly because of discontinued development of Bond, which is in that PR used as method of communication between nodes.

# Design

![](assets/rar-lifetime.png)

_Figure 1 Rough diagram of lifetime of service_

## Lifetime of service

### Connect to RAR node

Connecting to the RAR node will not require any discovery of processes on computer. The algorithm will follow these steps:

1. Get expected node name, which will be based on the current setting of node. The name format is described in `Start new node`.
2. Setup named pipe. The construction of named pipe may differ between platforms (usage of different API for construction of the pipe object).
3. Try to connect to the node.

If the connection is successful, we can use this connection for execution of RAR task. The node is guaranteed to have all required properties since they must be encoded in name of the RAR node.

### Start new node

This step will create new process which will act as RAR node. It will also pass necessary information to the node to know what its settings are (reusable, ...). Node will be another instance of the MSBuild.exe which will have set parameter **nodeMode** to some specific value (it should be `/nodeMode:3`). 

We will use named-pipe exclusivity to ensure we don't create multiple RAR nodes at once. Its name must encode whether it is the user's only RAR node, including user name, administrator privileges, and some initial settings for the node. Such a name could be: `MSBuild.RAR.ostorc.7`, where **MSBuild.RAR** is its prefix, **ostorc** is the user who called MSBuild, and **7** represents encoded settings (flag enum).

RAR Node will adopt existing MSBuild infrastructure code, NodeProviderOutOfProcTaskHost and related, used for invoking tasks out of process.

This code already solved many aspect of 'Out of process task invocation':
- serialization of task inputs and outputs
- distributed logging
- environmental variables
- current directory path
- current culture
- cancellation
- etc...

### Execute RAR task

Execution should be the same as it is now.

There is already some layer of separation between Task interface and actual execution method. We will leverage this, and put the decision logic if to run locally or not into the "wrapper" method and so we will not have to modify this and in server-side execution we will directly call the internal execution method.

#### RAR Concurrency

There is one big concern and that is how to handle multiple requests at once. As right now, RAR task is not prepared for multi-thread use.

One of the biggest challenges with RAR as service, is to make execution and caching of RAR task thread-safe, since in most cases there will be multiple clients requesting data from it at once.

So far, we have identified following areas that have to be addressed to allow concurrent execution of RAR tasks:

- thread safety (static variables, shared data structures, caching, ...)
- environmental variables virtualization
- current directory virtualization
- current culture isolation

### Shutdown RAR node

If the user does not want the node to be reused, we have the ensure that node will be killed after the build ends. This should be done after the main MSBuild node finishes building.

The RAR node, also has to support accepting of already established commands for MSBuild nodes (for example Shutdown command). This will be done by creating two pipes inside node, one will be for communication about RAR commands and second one for the servicing communication.

### Execute task in MSBuild node

User opted out of using the RAR nodes so we will execute the RAR task in the MSBuild node (as it is right now).

### Other

The new RAR node will not count toward total maximum CPU count specified by _/maxCpuCount_ switch, since the RAR task is taxing on IO operations not so much on CPU time. If we took one node from each instance of MSBuild it would lead to drastic decrease in performance.

The RAR task will be affected by the _/m_ switch. When we run in single node mode, it will implicitly say that we want to run the task inside current process. User would have to explicitly say that they want to use the RAR node.

__NOTE:__ The behavior described above depend on fact that the feature is opt-out (is active by default). If not, the paragraph above is meaningless. This has to be yet decided/clarified.

## Communication

The communication between nodes should be same as current cross node communication. RAR service will allow multiple net-pipe clients, each client session handled in separate thread.

## RAR service instrumentation

RAR will use same instrumentation infrastructure leveraged by standard MSBuild nodes. We will make sure we log all important events needed to measure, maintain and troubleshoot RAR service.

Instrumentation of RAR task execution will not change and will be handled by Out of process task infrastructure.

# Non-Goals

- File watchers: using them would decrease required IO operations when checking disc changes
- Aggressive pre-computation of results
- Improved caching of requests
- Providing verbosity to RAR task:
    As mentioned in original [PR](https://github.com/dotnet/msbuild/pull/3914), there should be some way to determine what thing we should log (by severity), and pass back to the original node.

    Providing the verbosity level to the task should not probably be part of this project, but the RAR node should be able to accept the required verbosity on its input. This verbosity level should be introduced into the RAR task by [#2700](https://github.com/dotnet/msbuild/issues/2700).
