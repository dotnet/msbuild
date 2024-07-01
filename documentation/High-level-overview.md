# What is MSBuild
MSBuild is a build platform used mainly for C# projects within .NET and Visual Studio. But when referencing MSBuild technically we can divide what MSBuild actually is in 3 main parts:
- Programming language that uses XML semantic to define build actions and data.
- API and command line program that interprets and manipulates the programming language.
- Build engine that executes a build based on the programmin language inputs.

MSBuild also contains some extensibility aspects that mostly interact with the API and engine. These are built to increase customization and interaction capability. 

This document covers all part of MSBuild in a general manner. So there will be no in depth technical details, or how-to's. If you'd like to learn how to use MSBuild to improve your builds please visit [Learn Microsoft](https://learn.microsoft.com/en-us/visualstudio/msbuild).


# MSBuild XML Language
The MSBuild programming language is a programming language that uses XML semantics with a focus on describing a project. You can see an [exmaple here](https://github.com/dotnet/msbuild/blob/main/src/Build/Microsoft.Build.csproj). 

The MSBuilkd XML is built around representing a project's data. It uses various attributes to do so:
- [Tasks](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) are how actions are defined in MSBuild, they're a unit of executable code to perform build operations. Most used tasks are defined within MSBuild itself, but can also be externally authored by implementing the `ITask` interface.
- [Targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets) represents a group os tasks, in which their order matters. It is a set of instructions for the MSBuild engine to build from.
- [Items](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-items) are inputs to the build system, mostly to tasks or targets. They can represent project files, code files, libraries and most things that a project can depend on.
- [Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties) are name value pairs, they're used to store data that is used throughout the build.

These attributes are defined within `.csproj` files, which are considered *projects*. `.sln` solution files can be normally found in .NET projects, are actually not written with MSBuild XML, but it is interpreted during project build so all projects can be identified.

Since the project file defines the data used for the build, the actual build instructions are imported through imports of libraries, that contains their own tasks and targets. One example that is vastly used is the SDK with `dotnet build`. These librearies also extend what can be done with a build, and overal functionality.

# MSBuild API
It is a library of common build logic that defined basic things, like the convept of output folder, intermediate folder, so other tools can utilize these resources. 
It is also a way to manipulate the MSBuild Language without directly changing the language on a project itself.
It is an API focused on building .NET programs so there are some specific things.
 - Common targets: 
Core idea behind those targets is you're either manipulating MSBuild data or figuring out the intermediate. What things should be passed to the compiler, or you are invoking tools 
    - like command line aplications (exe). We also offer base class toolTask to provide an interface for that.

## ToolTask
ToolTask is an interface offered by MSBuild to implement custom tasks. During the build, the MSBuild Engine will construct the task, call the execute method and let it run during execution. This process has performance advantages on windows when compared to writing a script to do the same work.

# Engine
The MSBuild Engine's main responsibility is to execute the build intructions and process the results of builds. Which includes managing the extensibilities modules that MSBuild offers, integrating them to this process even if they're authored by third parties.

Building a project can easily become a huge and time consuming project. To simplify things the MSBuild's engine logic is divided in two main stages: the evalution stage and the execution stage.

## Entry points
There a few officially supported entrypoints for the engine: Visual Studio, .NET SDK and the CLI executable (`MSBuild.exe`). All these methods are an implementation or extension of the MSBuild API. The inputs necessary to start a build include some specific build logic for the projects, generally given by the entry points, User defined imports, and the `.g.props` from NuGet restore. 

An example of that is the `<Project Sdk="Microsoft.NET.Sdk">` that can be seen in some of the built-in .NET templates. This indicates that the project will use build logic that comes with the .NET SDK.

## Evaluate operation
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

### imports

Complex projects generally include imports of many different types. In MSBuild an import can be a lot of things, a disk path, a property expansion, a known folder, or even environmental variables. There are also some main imports that come with the execution in other platforms, like the Visual Studio or SDK can have import directories that contain wild card imports. However, when it comes to the evaluation phase in MSBuild, imports are all treated like a property plus path expansion, this include imported NuGet packages.

In the case of tool imports, MSBuild does not process tool resolution via registry. Instead it is resolved by looking on adjacent folder to the current running version of MSBuild. The folders will be different depending is MSBuild is running from Visual Studio or the .NET SDK.

## Execution operation

For more detailed information on execution phase visit [Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview#execution-phase).

The execution phase is simply executing the targets defined in the XML by the user or implicitly defined by the SDK or VS. The order of executed targets is defined by the use of a few attributes: `BeforeTargets`, `DependsOnTargets`, and `AfterTargets`. But the final order might change if an earlier target modifies a property of a later target. The full executing order can be [found here](https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#determine-the-target-build-order).


### Executing a task
**TODO**
XML defines a task and you have a using element that has the configuration. When you execute the task, MSBuild looks up executing tasks, gets the DLL and what class to look at, and loads it with the properties that are present in the XML. Now we instantite an object of that class and of the task type (example ToolTask). We set a lot of properties that were defined in the XML (with potential task and item expansions) and then we call the execute method.

The execute method can do whatever it wants, including reportin back through an IBuildEngineInterface to do a few operations including logging.
And then we return.

### Task Host
MSBuild has a tool called Task Host, that allows tasks to run in a different .NET environment that the one used for build execution.

This is an opt-in behaviour that can be used for various cases:
- If a task breaks the build process it can be relegated to the Task Host so it does not influence the main build.
- If a task is built in the same repo that is currently being built by MSBuild and the code might change. So Task Host makes sure the DLLs are not lockd at the end of the build.

## Processes and nodes
When a new build is started MSBuild starts a process, which runs some setup code and prepares itself to start a build. This first node becomes the scheduler node and one of the worker nodes, becoming both the entry point for the project build and the scheduler. The main problem that arises from that, is when the whole build, the OS tears down the process, loosing the memory cache and having to restart the whole build process from the start. This is offset by having longer lived processes, that can be reused when building projects successionally.

## Caches
### Register Task Objects
During execution tasks might need to share state meaninfully between invocations in a single build or across builds. The MSBuild engine provides a mechanism to manage the lifetime of .NET objects to fill this gap. This lifetime can be defined by the user but has specific scopes: it can live per build or indefinetly. However, this mechanism is only available for communication within the same execution node, and it is not serializable.

### Project result cache
The project Result Cache refers to the cache used by the scheduler that keeps the build results of already executed project. The result of a taget is success, failure, and a list of items that succeeded. Beyond that the `return` and `output` attributes from targets are also serialized with the build result, as to be used by other targets for their execution.

### Project result cache plugin
This Project Cache differs from the previous because it is separate, and used mainly for distributed builds. It functions as a middle layer between the scheduler and the Project Result Cache. So when the scheduler requests a result for a target or project, the plugin responds first as to check all the different distributed nodes for the result. To accomplish this it adds profiles on disk based on hashes of the project or task ID / name. When the plugin cache gets a hit on an input, it downloads and copy the file results to the right place, desirializes the resulting payload and povides it to the local engine to continue execution.

For more in depth information visit [the spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/project-cache.md).

## Scheduler
The scheduler is the part of the MSBuild engine responsible for scheduling work to different nodes, as well as maintaining and managing the result of already executed projects. When a build starts, the scheduler assigns the entry point project to a working node (generally the in-proc node). The  project's execution starts and proceeds until the whole operation ends or is blocked. Once a node is not proceeding with the current project, either finished or blocked, the scheduler than access if it has more work to be given to that node, and assigns it.

On a project's operation end and returned result, it sends that information to the scheduler. The scheduler maintains results of all of the build's executed targets, so when a project or target depends on another to proceed execution, the scheduler can just retrieve that information from the Project Result Cache. Since the scheduler and project are generally in different processes, this communication happens within the engine using built-in loggers.

If a the node's operation is blocked by a dependency, it asks the scheduler for the results of the dependency's execution. If the dependency has been executed, the result is retrieved from the Project Result Cache. If the process has not been executed, the scheduler suspends the current execution, making the target / project a pending request. When a request is pending, the scheduler adds to the list of requests to execute, and assigns the dependency to be executed to either the current node or another one that is free.

### Incremental build
Incremental builds are extremely useful for local development, as it speeds consecutive builds on local machines. For this, the output from each project build are saved in memory, which becomes one big cache for MSBuild.

## Parallelism
Parallelism for MSBuild is implemented at project level. Each project is assigned to different working nodes, which will execute the tasks at the same time, with the Scheduler organizing sequence and work division. Within project, targets run sequentially, however they can have parallelism implemented independently from projects.

For multi-targeted builds parallelism works slightly differnt. The outer build produces a list of projects to build. This list contains the same project file with a different metadata for the target framework. This list is then passed to the MSBuild execute target so it can be built in parallel.


## IPC (interprocess communication)
In multiprocess MSBuild execution, many OS processes exist that need to communicate with each other. There are two main reasons:
 - Dealing with blocked tasks on processes: Communicating with the engine, scheduler, cache, etc...
 - Communication on task execution for a task host: Task definition, task inputs, task outputs.

## Graph build
A graph build changes the sequence in which MSBuild processes projects. Normally a project starts execution, and when it has a dependency on another project, then that project starts to build. A graph build evaluates all projects and their relationship before starting execution of any project. This is achieved by looking at specific items in the XML (like Project Reference) to contruct the dependency graph.

There are a couple of different modes to run graph mode in:
- Stardard mode: Tried to work from the leaves of the dependency graph and makes sure all results are within the cache. If there is a cache miss / unexpected reference, it just schedules the missing reference for execution.
- Strict / isolate mode: If there is a cache miss when building, the whole built is failed. This is used mostly for distributed system builds.

## MSbuid Server
In normal MSBuild execution the main process is cleared after the build ends, or after a set time limit. The MSBuild Server project aims to change that, making the entry point process and the schduler process node separate entities. This allows processed to preserve in-memory cache and make consecutive builds faster.

# Extensibilities
MSBuild includes some extra features that are related to the build process but does not fit on the previous categories. These extensibility features are critical for the build process, but they can also be customized by third parties for their own use.

## Packaging system
MSBuild interacts with external packages in almost every build. However the MSBuild engine does not recognize external packages as third parties, and it also does not handle external dependencies. This is done by a packaging system. The supported one being NuGet. As such, it is NuGet that is responsible for finding the packages, downloading them, and providing the MSBuild engine with a package path for the build.

## Restore
The restore operation is a built-in target within MSBuild. The main function is to walk through the project references and ` packages.config` file about all the packages that need to be restored. This process is executed by NuGet, as MSBuild does not have a packaging system within the code.

## Diagnosability / Loggers
Diagnosability within MSBuild went through some changes. Before we had a debugger, where you could step through the XML during the build and debug. This was discardted in favour of a log focused approach, where MSBuild has a more robust logging system that contains more data to identify what is happening during a build.

Beyond loggers, we have some ETW (Event Tracing for Windows) events which can also be identified through loggers.

### General Loggers
Logging within MSBuild consists of various integrated and pluggable loggers. Integrated loggers generally processes code structure events, such as communication between nodes during build, or data for BuildCheck analyzers to run properly.Built-in loggers include the Binary Logger, Console / Terminal logger, and a Text Log. Pluggable loggers are third party loggers that can receive events through the MSBuild API, or the .NET event handlers.

Pluggable loggers are added through DLLs, and MSBuild engine identifies them at the beginning of the build. Because of this, build logic is not able to manipulate loggers.

### Binary logger
Implemetation is based around the communication and serialization protocal that MSBuild uses.
This is not officially supported by us, but it is one of the most used tools for debugging. It is considered a pluggable logger.

## Resolvers
There are a few elements within the MSBuild SML that indicate that a call to the .NET SDK is necessary. Some exaples include:
 - `<Project Sdk="Microsoft.NET.Sdk">`, where you can also define the SDK version
 - `<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />`, for explicit imports.

When such interaction is necesasary for a project build, the first thing that needs to be done is to figure out where the SDK is installed so MSBuild can access the content. This is solved by resolvers, which look for the SDK version that was specified, or gets the latest version.

To read more abou SDK resolver you can check the [Microsoft Learn page](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk#how-project-sdks-are-resolved), or see the [spec documentation](https://github.com/dotnet/msbuild/blob/main/documentation/specs/sdk-resolvers-algorithm.md).

## Telemetry
MSBuild has a few telemetry points, moslty through the .NET SDK. It is implemented as a logger that keeps track of telemtry events in the SDK, this allows to have a single opt-out mechanism that also works for MSBuild.

Visual Studio telemetry was removed once MSBuild went open source, and it was never added again.
