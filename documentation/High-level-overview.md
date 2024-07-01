# What is MSBuild
MSBuild is a build platform used mainly for C# projects within .NET and Visual Studio. But when referencing MSBuild technically we can divide what MSBuild is in 3 main parts:
- Programming language that uses XML semantics to define build actions and data.
- API and command line program that interprets and manipulates the programming language.
- Build engine that executes a build based on the programming language inputs.

MSBuild also contains some extensibility aspects that mostly interact with the API and engine. These are built to increase customization and interaction capability. 

This document covers all parts of MSBuild in a general manner. So, there will be no in depth technical details, or how-to's. If you'd like to learn how to use MSBuild to improve your builds please visit [Learn Microsoft](https://learn.microsoft.com/en-us/visualstudio/msbuild).


# MSBuild XML Language
The MSBuild programming language is a programming language that uses XML semantics with a focus on describing a project. You can see an [exmaple here](https://github.com/dotnet/msbuild/blob/main/src/Build/Microsoft.Build.csproj). 

The MSBuilkd XML is built around representing a project's data. It uses various attributes to do so:
- [Tasks](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) are how actions are defined in MSBuild, they're a unit of executable code to perform build operations. Most used tasks are defined within MSBuild itself but can also be externally authored by implementing the `ITask` interface.
- [Targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) represents a group of tasks, in which their order matters. It is a set of instructions for the MSBuild engine to build from.
- [Items](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-items) are inputs to the build system, mostly to tasks or targets. They can represent project files, code files, libraries and most things that a project can depend on.
- [Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties) are name value pairs, they're used to store data that is used throughout the build.

These attributes are defined within `.csproj` files, which are considered *projects*. `.sln` solution files can be normally found in .NET projects, are not written with MSBuild XML, but it is interpreted during project build so all projects can be identified.

Since the project file defines the data used for the build, the actual build instructions are imported through imports of libraries, that contains their own tasks and targets. One example that is vastly used is the SDK with `dotnet build`. These libraries also extend what can be done with a build, and overall functionality.

# MSBuild API
The MSBuild API is a library with a focus on building .NET programs, as such it is used by Visual Studio and .NET SDK to integrate MSBuild as their project build system. The library includes common build logic and targets, like creation and management of output folder, intermidiary folders, custom task creation, etc... It also enables the change of the MSBuild Language without directly changing the project file itself.

## ToolTask
ToolTask is an interface offered by MSBuild to implement custom tasks. During the build, the MSBuild Engine will construct the task, call the execute method and let it run during execution. This process has performance advantages on windows when compared to writing a script to do the same work.

# Engine
The MSBuild Engine's main responsibility is to execute the build instructions and process the results of builds. Which includes managing the extensibilities modules that MSBuild offers, integrating them into this process even if they're authored by third parties.

Building a project can easily become a huge and time-consuming project. To simplify things the MSBuild's engine logic is divided into two main stages: the evalution stage and the execution stage.

## Entry points
There are a few officially supported entry points for the engine: Visual Studio, .NET SDK and the CLI executable (`MSBuild.exe`). All these methods are an implementation or extension of the MSBuild API. The inputs necessary to start a build include some specific build logic for the projects, generally given by the entry points, User defined imports, and the `.g.props` from NuGet restore. 

An example of that is the `<Project Sdk="Microsoft.NET.Sdk">` that can be seen in some of the built-in .NET templates. This indicates that the project will use build logic that comes with the .NET SDK.

## Evaluate operation
For more detailed information on evaluation visit [Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview#evaluation-phase).

Evaluation of the build is the first step of the process. Its main objective is to collect information on the project being built. This includes checking entry point, imports, items, and tasks. Additionally, for Visual Studio it also gathers information about which C# files, solution files and project files are checked in the IDE.

The first step of evaluation is to load the project file and the XML data it contains. There are multiple passes within the same file to collect data, some of those to specifically define project properties and imports that are necessary for other tasks. At this time, the restore target has run already, so all imports are files on disk and are processed as paths by the engine. Another characteristic of imports is that they are brough within the project logic, so other projects can refence the same import logic instead of having a copy of the same data. Data loaded within the evaluation are not used until execution stage. This means that data can be added and modified during evaluation. 

The evaluation stage should not have any side effect on disk, no new or deleted files. Two exceptions for this are:
 - SDK resolution
 - NuGet SDK, which might add packages to the disk

### imports
Complex projects generally include imports of many different types. In MSBuild an import can be a lot of things, a disk path, a property expansion, a known folder, or even environmental variables. There are also some main imports that come with the execution on other platforms, like the Visual Studio or SDK can have import directories that contain wild card imports. However, when it comes to the evaluation phase in MSBuild, imports are all treated like a property plus path expansion, this includes imported NuGet packages.

In the case of tool imports, MSBuild does not process tool resolution via registry. Instead, it is resolved by looking on adjacent folder to the current running version of MSBuild. The folders will be different depending is MSBuild is running from Visual Studio or the .NET SDK.

## Execution operation
For more detailed information on execution phase visit [Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview#execution-phase).

The execution phase is simply executing the targets defined in the XML by the user or implicitly defined by the SDK or VS. The order of executed targets is defined using a few attributes: `BeforeTargets`, `DependsOnTargets`, and `AfterTargets`. But the final order might change if an earlier target modifies a property of a later target. The full executing order can be [found here](https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#determine-the-target-build-order).

### Task Host
MSBuild has a tool called Task Host, that allows tasks to run in a different .NET environment than the one used for build execution.

This is an opt-in behavior that can be used for various cases:
- If a task breaks the build process it can be relegated to the Task Host, so it does not influence the main build.
- If a task is built in the same repo that is currently being built by MSBuild and the code might change. So, Task Host makes sure the DLLs are not locked at the end of the build.

## Processes and nodes
When a new build is started MSBuild starts a process, which runs some setup code and prepares itself to start a build. This first node becomes the scheduler node and one of the worker nodes, becoming both the entry point for the project build and the scheduler. The main problem that arises from that is when the whole build, the OS tears down the process, losing the memory cache and having to restart the whole build process from the start. This is offset by having longer lived processes, that can be reused when building projects successionally.

## Caches
### Register Task Objects
During execution tasks might need to share state meaningfully between invocations in a single build or across builds. The MSBuild engine provides a mechanism to manage the lifetime of .NET objects to fill this gap. This lifetime can be defined by the user but has specific scopes: it can live per build or indefinitely. However, this mechanism is only available for communication within the same execution node, and it is not serializable.

### Project result cache
The project Result Cache refers to the cache used by the scheduler that keeps the build results of already executed project. The result of a target is success, failure, and a list of items that succeeded. Beyond that the `return` and `output` attributes from targets are also serialized with the build result, as to be used by other targets for their execution.

### Project result cache plugin
This Project Cache differs from the previous one because it is separate from the main MSBuild code and used mainly for distributed builds. It functions as a middle layer between the scheduler and the Project Result Cache. So, when the scheduler requests a result for a target or project, the plugin responds first to check all the different distributed nodes for the result. To accomplish this, it adds profiles on disk based on hashes of the project or task ID / name. When the plugin cache gets a hit on an input, it downloads and copies the file results to the right place, deserializes the resulting payload and provides it to the local engine to continue execution.

For more in depth information visit [the spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/project-cache.md).

## Scheduler
The scheduler is the part of the MSBuild engine responsible for scheduling work to different nodes, as well as maintaining and managing the result of already executed projects. When a build starts, the scheduler assigns the entry point project to a working node (generally the in-proc node). The project's execution starts and proceeds until the whole operation ends or is blocked. Once a node is not proceeding with the current project, either finished or blocked, the scheduler then access if it has more work to be given to that node and assigns it.

On a project's operation end and returned result, it sends that information to the scheduler. The scheduler maintains results of all of the build's executed targets, so when a project or target depends on another to proceed execution, the scheduler can just retrieve that information from the Project Result Cache. Since the scheduler and project are generally in different processes, this communication happens within the engine using built-in loggers.

If the node's operation is blocked by a dependency, it asks the scheduler for the results of the dependency's execution. If the dependency has been executed, the result is retrieved from the Project Result Cache. If the process has not been executed, the scheduler suspends the current execution, making the target / project a pending request. When a request is pending, the scheduler adds to the list of requests to execute, and assigns the dependency to be executed to either the current node or another one that is free.

### Incremental build
Incremental builds are extremely useful for local development, as it speeds consecutive builds on local machines. For this, the output from each project build is saved in memory, which becomes one big cache for MSBuild.

## Parallelism
Parallelism for MSBuild is implemented at project level. Each project is assigned to different working nodes, which will execute the tasks at the same time, with the Scheduler organizing sequence and work division. Within project targets run sequentially, however they can have parallelism implemented independently from projects.

For multi-targeted builds parallelism works slightly different. The outer build produces a list of projects to build. This list contains the same project file with a different metadata for the target framework. This list is then passed to the MSBuild execute target so it can be built in parallel.


## IPC (inter-process communication)
In multi-process MSBuild execution, many OS processes exist that need to communicate with each other. There are two main reasons:
 - Dealing with blocked tasks on processes: Communicating with the engine, scheduler, cache, etc...
 - Communication on task execution for a task host: Task definition, task inputs, task outputs.

## Graph build
A graph build changes the sequence in which MSBuild processes projects. Normally a project starts execution, and when it has a dependency on another project, then that project starts to build. A graph build evaluates all projects and their relationship before starting execution of any project. This is achieved by looking at specific items in the XML (like Project Reference) to construct the dependency graph.

There are a couple of different modes to run graph mode in:
- Standard mode: Tried to work from the leaves of the dependency graph and makes sure all results are within the cache. If there is a cache miss / unexpected reference, it just schedules the missing reference for execution.
- Strict / isolate mode: If there is a cache miss when building, the whole built is failed. This is used mostly for distributed system builds.

## MSBuid Server
In normal MSBuild execution the main process is cleared after the build ends, or after a set time limit. The MSBuild Server project aims to change that, making the entry point process and the scheduler process node separate entities. This allows processes to preserve in-memory cache and make consecutive builds faster.

# Extensibilities
MSBuild includes some extra features that are related to the build process but does not fit on the previous categories. These extensibility features are critical for the build process, but they can also be customized by third parties for their own use.

## Packaging system
MSBuild interacts with external packages in almost every build. However, the MSBuild engine does not recognize external packages as third parties, and it also does not handle external dependencies. This is done by a packaging system. The supported one being NuGet. As such, it is NuGet that is responsible for finding the packages, downloading them, and providing the MSBuild engine with a package path for the build.

## Restore
The restore operation is a built-in target within MSBuild. The main function is to walk through the project references and `packages.config` file about all the packages that need to be restored. This process is executed by NuGet, as MSBuild does not have a packaging system within the code.

## Diagnosability / Loggers
Diagnosability within MSBuild went through some changes. Before we had a debugger, where you could step through the XML during the build and debug. This was discarded in favor of a log focused approach, where MSBuild has a more robust logging system that contains more data to identify what is happening during a build.

Beyond loggers, we have some ETW (Event Tracing for Windows) events which can also be identified through loggers.

### General Loggers
Logging within MSBuild consists of various integrated and pluggable loggers. Integrated loggers generally processes code structure events, such as communication between nodes during build, or data for BuildCheck analyzers to run properly. Built-in loggers include the Binary Logger, Console / Terminal logger, and a Text Log. Pluggable loggers are third party loggers that can receive events through the MSBuild API, or the .NET event handlers.

Pluggable loggers are added through DLLs, and MSBuild engine identifies them at the beginning of the build. Because of this, build logic is not able to manipulate loggers.

### Binary logger
The Binary Logger, also called BinLog, is a structured log that contains all the events within a build. It achieves that through its implementation focused on reading events from the build and serializing those in an structured form. To read a BinLog the BinLog reader can be used, but it is not officially supported by the MSBuild team.
It is one of the best tools for debugging MSBuild. 

## Resolvers
There are a few elements within the MSBuild SML that indicate that a call to the .NET SDK is necessary. Some examples include:
 - `<Project Sdk="Microsoft.NET.Sdk">`, where you can also define the SDK version
 - `<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />`, for explicit imports.

When such interaction is necessary for a project build, the first thing that needs to be done is to figure out where the SDK is installed so MSBuild can access the content. This is solved by resolvers, which look for the SDK version that was specified, or gets the latest version.

To read more about SDK resolver you can check the [Microsoft Learn page](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk#how-project-sdks-are-resolved), or see the [spec documentation](https://github.com/dotnet/msbuild/blob/main/documentation/specs/sdk-resolvers-algorithm.md).

## Telemetry
MSBuild has a few telemetry points, mostly through the .NET SDK. It is implemented as a logger that keeps track of telemetry events in the SDK, this allows to have a single opt-out mechanism that also works for MSBuild.

Visual Studio telemetry was removed once MSBuild went open source, and it was never added again.
