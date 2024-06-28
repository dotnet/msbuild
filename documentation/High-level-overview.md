# MsBuild high level overview

## What is MSBuild
MSBuild is a build platform used mainly for C# projects within .NET and Visual Studio. But when referencing MSBuild technically we can divide what MSBuild actually is in 3 main parts:
- Programming language that uses XML semantic to define build actions and data.
- API and command line program that interprets and manipulates the programming language.
- Build engine that executes a build based on the programmin language inputs.

MSBuild also contains some extensibility aspects that mostly interact with the API and engine. These are built to increase customization and interaction capability. 

This document covers all part of MSBuild in a general manner. So there will be no in depth technical details, or how-to's. If you'd like to learn how to use MSBuild to improve your builds please visit [Learn Microsoft](https://learn.microsoft.com/en-us/visualstudio/msbuild).


## MSBuild XML Language
The MSBuild programming language is a programming language that uses XML semantics with a focus on describing a project. You can see an [exmaple here](https://github.com/dotnet/msbuild/blob/main/src/Build/Microsoft.Build.csproj). 

The MSBuilkd XML is built around representing a project's data. It uses various attributes to do so:
- [Tasks](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) are how actions are defined in MSBuild, they're a unit of executable code to perform build operations. Most used tasks are defined within MSBuild itself, but can also be externally authored by implementing the `ITask` interface.
- [Targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) represents a group os tasks, in which their order matters. It is a set of instructions for the MSBuild engine to build from.
- [Items](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-items) are inputs to the build system, mostly to tasks or targets. They can represent project files, code files, libraries and most things that a project can depend on.
- [Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties) are name value pairs, they're used to store data that is used throughout the build.

These attributes are defined within `.csproj` files, which are considered *projects*. `.sln` solution files can be normally found in .NET projects, are actually not written with MSBuild XML, but it is interpreted during project build so all projects can be identified.

Since the project file defines the data used for the build, the actual build instructions are imported through imports of libraries, that contains their own tasks and targets. One example that is vastly used is the SDK with `dotnet build`. These librearies also extend what can be done with a build, and overal functionality.

## MSBuild API
**to add mote information**

It is a library of common build logic that defined basic things, like the convept of output folder, intermediate folder, so other tools can utilize these resources. 
It is also a way to manipulate the MSBuild Language without directly changing the language on a project itself.
It is an API focused on building .NET programs so there are some specific things.
 - Common targets: 
Core idea behind those targets is you're either manipulating MSBuild data or figuring out the intermediate. What things should be passed to the compiler, or you are invoking tools 
    - like command line aplications (exe). We also offer base class toolTask to provide an interface for that.

### ToolTask
ToolTask is an interface offered by MSBuild to implement custom tasks. During the build, the MSBuild Engine will construct the task, call the execute method and let it run during execution. This process has performance advantages on windows when compared to writing a script to do the same work.

## Engine
The MSBuild Engine's main responsibility is to execute the build intructions and process the results of builds. Which includes managing the extensibilities modules that MSBuild offers, integrating them to this process even if they're authored by third parties.

Building a project can easily become a huge and time consuming project. To simplify things the MSBuild's engine logic is divided in two main stages: the evalution stage and the execution stage.

### Entry points
There a few officially supported entrypoints for the engine: Visual Studio, .NET SDK and the CLI executable (`MSBuild.exe`). All these methods are an implementation or extension of the MSBuild API. The inputs necessary to start a build include some specific build logic for the projects, generally given by the entry points, User defined imports, and the `.g.props` from NuGet restore. 

An example of that is the `<Project Sdk="Microsoft.NET.Sdk">` that can be seen in some of the built-in .NET templates. This indicates that the project will use build logic that comes with the .NET SDK.

### Evaluate operation
For more detailed information on evaluation visit [Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview#evaluation-phase).

**TODO**
The evaluation phase of a build is where the engine gathers information on the projects. This includes entry points, imports, items, and tasks. For VS IDE purposes, it also gathers information about which C# files are checked in, solution files, etc...

The first step of evaluation is to load the project and the XML data that it contains. To gather all the data there are multiple passes through the project file, some of them to specifically define project properties and imports. 
At this stage of evaluation all imports are files on disk, which are processed as paths by the engine. From those imports the build logic is attached to the current processed project, and this allows

The build and processing logic from imports is also brough to the current project, this way MSBuild avoids

Loads the project, read from XML. Has multiple passes through the project file, and it will specifically define properties and imports.
Import finds the file on disk, take the build logic from there and bring it to the current executing project like it was there in the first place, so we avoid every c# project having details about the c# compiler invokation. Define it in one place and import to the rest.
Evaluation should have no side effects on disk, just an in memory representation of that project. The exception is the SDK resolution, in which new files might appear on disk.
 - One example is the Nuget SDK, that is commonly involved in builds. Will have side effects during evaluation, like NuGet packages appearing on your disk.

 All the data present does not mean anything until exedcution point. Which means that during evaluation you can modify or add data to properties and items. This is a critical part of how projects are built. For example, it is common to generate a C# file based on a part of your build. This also includes intermediate data or output.

 So running sequence is: imports (which can honestly be anything, even environmental variables), item definitions, items, tasks and targets. But nothing really executed. We do have a white list of used things that prevent side effects.

#### imports

Complex projects generally include imports of many different types. In MSBuild an import can be a lot of things, a disk path, a property expansion, a known folder, or even environmental variables. There are also some main imports that come with the execution in other platforms, like the Visual Studio or SDK can have import directories that contain wild card imports. However, when it comes to the evaluation phase in MSBuild, imports are all treated like a property plus path expansion, this include imported NuGet packages.

In the case of tool imports, MSBuild does not process tool resolution via registry. Instead it is resolved by looking on adjacent folder to the current running version of MSBuild. The folders will be different depending is MSBuild is running from Visual Studio or the .NET SDK.

### Execution operation

For more detailed information on execution phase visit [Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview#execution-phase).

The execution phase is simply executing the targets defined in the XML by the user or implicitly defined by the SDK or VS. The order of executed targets is defined by the use of a few attributes: `BeforeTargets`, `DependsOnTargets`, and `AfterTargets`. But the final order might change if an earlier target modifies a property of a later target. The full executing order can be [found here](https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#determine-the-target-build-order).


#### Executing a task
**TODO**
XML defines a task and you have a using element that has the configuration. When you execute the task, MSBuild looks up executing tasks, gets the DLL and what class to look at, and loads it with the properties that are present in the XML. Now we instantite an object of that class and of the task type (example ToolTask). We set a lot of properties that were defined in the XML (with potential task and item expansions) and then we call the execute method.

The execute method can do whatever it wants, including reportin back through an IBuildEngineInterface to do a few operations including logging.
And then we return.

#### Task Host
**TODO**
When a task needs to run in a different .NET invironment than the one currently in, we handle that through the Task Host.

MSBuild starts a new process of the correct environment, uses an IPC mechanism to feed information for that task, run it in another process, and get information back.

Tasks can ask the host engine for task relevant objects and use it. This is a way to communicate between nodes and tasks across the build.

You can opt-in to this behaviour.
Where you might want to do this:
 - When a task misbehaves and breaks the whole process
 - You have a task that is built in the same repo you're building your code and might change between runs. In this case we make sure that the DLLs are not locked at the end of the building. (Sandbox execution)

### Processes and nodes
When a new build is started MSBuild starts a process, which runs some setup code and prepares itself to start a build. This first node becomes the scheduler node and one of the worker nodes, becoming both the entry point for the project build and the scheduler. The main problem that arises from that, is when the whole build, the OS tears down the process, loosing the memory cache and having to restart the whole build process from the start. This is offset by having longer lived processes, that can be reused when building projects successionally.

### Cache
**TODO**
Special property or something that points to a dll that implements the cache interface. Like a plugin, so it would work out of a nuget package.
Task host cache (described more in the task host section (link))

#### Register Task Objects
**TODO**
If you are a tak and you need to meaningfully share state between invocations of said task in a single build or across builds the engine provides a mechanism to manage the lifetime of a .net object. When defining scope of objecdt's lifetime if can be per build or indefinetly.
This is available per node basis if you need to use it.
There is no serialization or desirialization, just a caching mechanism in memory.

#### Project result cache
**TODO**
(Are there toher types of cache?)
Cache keeps the results of built projects / tasks (project result cache). Cache is mostly on memory but can spill on disk.
RAR state cache has some custom on disk format.
Lives in the scheduler layer

Build results are stored as items 
The return and output attributes from a targets already serialize the results from said task. The result of a task is generally, sucess, failure and a list of items that succeeded.

#### Project result cache plugin
**TODO**
It was mainly use for distributed builds like CloudBuild.
Works as a middle layer between the scheduler and the cache, which allows more functionallity to be pluged in there. So scheduler asks t if they can satisfy node requests for project results.
Also adds profiles on disk, so it knows where to look for reults when asked.
We hash the inputs so we can create those profiles, and the plugin uses those to find the results and output of the request.
When it gets a hit, it downloads and copy the files to the right place, desirialize the the result payload and provide it to the engine.

#### Incremental build
**TODO**
All the output from the builds that are in memory becomes one big cache for MSBuild. The targets inputs and outputs that is what get's cached / saved. We don't really know how it works, oops.

### Scheduler
**TODO**
Something the schedules the execution of chunks of projects (targets or projects).
Scheduler maintains results of executed targets., as well as remembering which projects were already built and have their results. When a result is asked from a task / project that was built previously it gets it from the cache.
When communicating within the process. A task talks to the engine, which then communicates with the scheduler, to either get results or be blocked.

Build starts with an entry point. Scheduler assigns the entry point to a node (generally the in-proc node). Execution starts and targets start builds in some order. When an operation ends, they provide the results to the scheduler. The scheduler does not care about what the targets do, just what they return once executed.

It can also see pending requests. For example, when one task depends on another, in the middle of task execution, the node will tell the scheduler that it cannot complete, and it needs a list of requests of what it needs. The scheduler can either satisfy those requests from cache, or it puts it on the list for execution.

When a node is finished or blocked, the scheduler consider if they can assign more work to a specific node. And that is how out multiprocess stuff become parallelism.

#### MSBuild Task
**TODO**
We don't get it as a task but as an intrinsic method. Almost the same but different?

### Parallelism
**TODO**
Tasks run sequentially.

Parallelism is at the project level. Each project is single threaded until the process yields o MSBuild passes a task call.
MSBuild keeps control of the level of parallelism. Tasks however can implement parallelism indepedendt of projects.

For multi targeted builds, parallelism can be achieved but there are some extra steps. The outer build produces a list where the include of the list is the same project file each time, but different metadata for the target frameworks, and that is passed to the single MSBuild task so that it can build it in parallel.

#### Batching
**TODO**
Batching can be done in the targets and task levels.


*A lot of the talk here is what we could do, idk if it appl;ies too much right now*


### IPC (interprocess communication)
**TODO**
During execution we have different OS processes that need to talk with each other.
 - Command line inout
 - Console output
 - Connection between scheduler and worker node
    - Go build this project
    - locked bc we need the result from another build

If a node is blocked on a specific project, it is considered freed to be used in other projects as the scheduler allows.

Nature of messages:
 - Deal with blocked tasks on processes (the whole engine communication, freeing a process / nod, etc..)
 - Communication on task execution for a task host
    - Task definition
    - inputs
    - give me the outputs

Transport layer:
They are based on biderectional BCL .net pipe implementation. In windows they are named pipes, and it has a unix implementation that wraps around sockets.

Since in unix the namespace for pipes and custom code is the same, we create an intermidiate layer with a temporary folder based on the temp environmental variable.

Message Layer:
Is a custom serialization protocol that is MSBuild specific. Dsigned to be easy to implement new types (ITranslatable). All the types are known internal MSBuild types, with extra dictionary fields to support user custom strings.

### Graph build
**TODO**
Try to evaluate all projects upfront and form relationships between them. So, projects get evaluated, but instead of going and building it, we look at the evaluated project and try to figure out the other projects it references.
Looks at specific items like Project Refence in order to construct a dependency graph.

The graph itself is not serialized today.
This whole thing is similar to NuGet graph restore.

There is a special scheduler for graph builds, it does not replace the existing scheduler but auguments it. It basically sends build requests to the scheduler when all the project dependencies are satisfied.

In case of different evaluation time graph and execution time behaviour of the project not matching:
- Standard mode: It tries to work from the leaves and makes sure that all the results are within the cache. If it hits an unexpected reference, it just tried to schedule that reference for build.
- Strict / isolate mode: If there is a cache miss, meaning a dependency has not been built already. Just fail the whole build.

With this second mode you need to specify input and output results cache so different projects are able to access the results of dependencies. This is used in distributed builds (CloudBuild for example).



### MSbuid Server
**TODO**
*Watch the knowledge hand off to get more information about MSBuild server*

The MSBuild server is the idea of separating some processes. Splitting the entry point executable that lives for one build invocation, and the scheduler and the 1st in-proc node. This way you don't need to JIT and you can preserve your in-memory cache.



## Extensibilities
**TODO**

MSBuild includes some extensibilities to it's main process. There were added to support MSBuild in various scenarios, as well as improve user experience with the build process.


We have some extensibilities within MSBuild:
This includes some built-in tasks: Exec, and Factories.
Resolvers are just devdiv / dotnet kinda thing
    Discover resolvers on a folder next to MSBuild. Mitigation to threats is installing MSBuild to an admin only 

### Packaging system
MSBuild interacts with external packages in almost every build. However the MSBuild engine does not recognize external packages as third parties, and it also does not handle external dependencies. This is done by a packaging system. The supported one being NuGet. As such, it is NuGet that is responsible for finding the packages, downloading them, and providing the MSBuild engine with a package path for the build.

### Diagnosability / Loggers
Diagnosability within MSBuild went through some changes. Before we had a debugger, where you could step through the XML during the build and debug. This was discardted in favour of a log focused approach, where MSBuild has a more robust logging system that contains more data to identify what is happening during a build.

Beyond loggers, we have some ETW (Event Tracing for Windows) events which can also be identified through loggers.

#### General Loggers
Logging within MSBuild consists of various integrated and pluggable loggers. Integrated loggers generally processes code structure events, such as communication between nodes during build, or data for BuildCheck analyzers to run properly.Built-in loggers include the Binary Logger, Console / Terminal logger, and a Text Log. Pluggable loggers are third party loggers that can receive events through the MSBuild API, or the .NET event handlers.

Pluggable loggers are added through DLLs, and MSBuild engine identifies them at the beginning of the build. Because of this, build logic is not able to manipulate loggers.

#### Binary logger
Implemetation is based around the communication and serialization protocal that MSBuild uses.
This is not officially supported by us, but it is one of the most used tools for debugging. It is considered a pluggable logger.

### Resolvers
There are a few elements within the MSBuild SML that indicate that a call to the .NET SDK is necessary. Some exaples include:
 - `<Project Sdk="Microsoft.NET.Sdk">`, where you can also define the SDK version
 - `<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />`, for explicit imports.

When such interaction is necesasary for a project build, the first thing that needs to be done is to figure out where the SDK is installed so MSBuild can access the content. This is solved by resolvers, which look for the SDK version that was specified, or gets the latest version.

To read more abou SDK resolver you can check the [Microsoft Learn page](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk#how-project-sdks-are-resolved), or see the [spec documentation](https://github.com/dotnet/msbuild/blob/main/documentation/specs/sdk-resolvers-algorithm.md).
    

### Restore
**TODO**
The restore operation is a built-in target within MSBuild. It goes through 

restore walks through the graph of projects to be built and determines all items that need to be restored, it then feeds the information into the restore task.
Restore is primarily a built-in target.
Restore has 2 modes:
- Normal MSBuild operation, walk the graph of projects to determine all things that need to be restored and then feed that into the restore task.
- Graph operations, which is done in a separate process, which calls our graph API to load the graph build.

The package restore is done by NuGet, not within MSBuild scope of responsbility, since engine does not understand "packages".

### Telemetry
MSBuild has a few telemetry points, moslty through the .NET SDK. It is implemented as a logger that keeps track of telemtry events in the SDK, this allows to have a single opt-out mechanism that also works for MSBuild.

Visual Studio telemetry was removed once MSBuild went open source, and it was never added again.
