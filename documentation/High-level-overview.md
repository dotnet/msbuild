# MsBuild high level overview

## What is MSBuild

MSBuild is 3 things, and when reffering to MSBuild you need to know which part you are reffering to.
- Programming language in XML semantic
- Build engine: it is more general then make for example
- API and command line program to interpret the programming language, and a library o
    - API for manipulating the programming language itself (VS uses it, by their properties UI).
- More extensibilities
    - Loggers, custom dlls, some other stuff, tasks (Msbuild task,)
    - Factories
    - there is also exec


## In-line vs VS builds

## Extensibilities
We have some extensibilities within MSBuild:
This includes some built-in tasks: Exec, and Factories.
Resolvers are just devdiv / dotnet kinda thing
    Discover resolvers on a folder next to MSBuild. Mitigation to threats is installing MSBuild to an admin only location. No way to specify a custom resolver.

### Packaging system
MSBiuld actually does not understand packages and packaging systems. We use NuGet as apackaging system for that function. It downloads, unzips and then provides a path to MSBuild to that package.

### Diagnosability
Once upon a time we had a debugger, but we deprecated in favor of a more robust logging system.
We focus on logs for debuggability.

Engine writes some ETW (event tracing for windows) events

### Loggers
Build logic cannot manipulate loggers, you have to pass the DLL.
We build our logging system based on events, we have a bunch of event types. Some of them are special and used by Binary Logger or BuildCheck, where we focus con code structure events.

All our events need to be serializable to go to binary log and to keep communication with the main node.

Beyond the integrated loggers, there can also be pluggable loggers that can receive events through .net event handlers.:
- Binary logger
- Console/terminal logger
- Text log

#### Binary logger
Implemetation is based around the communication and serialization protocal that MSBuild uses.
This is not officially supported by us, but it is one of the most used tools for debugging. It is considered a pluggable logger.

### Resolvers


### Restore
Restore is primarily a built-in target.
Restore has 2 modes:
- Normal MSBuild operation, walk the graph of projects to determine all things that need to be restored and then feed that into the restore task.
- Graph operations, which is done in a separate process, which calls our graph API to load the graph build.

The package restore is done by NuGet, not within MSBuild scope of responsbility, since engine does not understand "packages".

### Telemetry
We have telemetry points / information thought the .net SDK but not through VS.
It is basically a special logger that pays attention to telemetry events, which allows the CLI to have a single telemtry opt-out that applies to MSBuild without too much work on our part.


## Engine
When we commonly say "Build a project" we actually mean in MSBuild, evaluate and then build what you found.

### Processes
When you start executing MSBuild from the command line / exe. You start a process, which runs some code, starts to build stuff in that process, and then that process becomes the scheduler node and one of the worker nodes. So it is both the entry point and the scheduler.
The problem is that when that process ends, it exits and the OS tears it down. No more meory cache, we have kittering (what is this?), and for the next build you have to redo everything.

### Entry points
We have a few entrypoints to the MSBuild engine / where MSBuild is executed, but it is manily VS, SDK and CLI. All of those go through the API to access the engine.

As input to  everything we have:
 - Built in logic and imports (SDK / VS).
 - User defined imports.
 - .g.props from NuGet restore imports.

### Evaluate operation
*this is not a build*
"Tell me about this project" , where the project is the entry point, data, etc....
 - For IDE purposes, it also tells which C# files are checked in, project name etc...


Loads the project, read from XML. Has multiple passes through the project file, and it will specifically define properties and imports.
Import finds the file on disk, take the build logic from there and bring it to the current executing project like it was there in the first place, so we avoid every c# project having details about the c# compiler invokation. Define it in one place and import to the rest.
Evaluation should have no side effects on disk, just an in memory representation of that project. The exception is the SDK resolution, in which new files might appear on disk.
 - One example is the Nuget SDK, that is commonly involved in builds. Will have side effects during evaluation, like NuGet packages appearing on your disk.

 All the data present does not mean anything until exedcution point. Which means that during evaluation you can modify or add data to properties and items. This is a critical part of how projects are built. For example, it is common to generate a C# file based on a part of your build. This also includes intermediate data or output.

 So running sequence is: imports (which can honestly be anything, even environmental variables), item definitions, items, tasks and targets. But nothing really executed. We do have a white list of used things that prevent side effects.

#### imports
More in depth explanation of all the imports we handle.
Imports can be anything. Can be a path, property expansions, properties fallback on environmental variables, known folder that you can access.
There are globbed imports, like the VS directory and sdk that have import folders with wild card imports.
From the MSBuild perspective, when we get to imports, for example NuGet imports. Things are not different than any other import, which lokks like a property plus path expansion.
Core MSBuild does not do tool resolution via registry, just use the copy of the tools that is in a folder next to us. However, there are functions that read the registry and the some SDKs use those.

### Execution operation
Requires evaluation operation. When we commonly say "Build a project" we actually mean in MSBuild, evaluate and then build what you found.

#### Executing a task
XML defines a task and you have a using element that has the configuration. When you execute the task, MSBuild looks up executing tasks, gets the DLL and what class to look at, and loads it with the properties that are present in the XML. Now we instantite an object of that class and of the task type (example ToolTask). We set a lot of properties that were defined in the XML (with potential task and item expansions) and then we call the execute method.

The execute method can do whatever it wants, including reportin back through an IBuildEngineInterface to do a few operations including logging.
And then we return.

#### Task Host
When a task needs to run in a different .NET invironment than the one currently in, we handle that through the Task Host.

MSBuild starts a new process of the correct environment, uses an IPC mechanism to feed information for that task, run it in another process, and get information back.

Tasks can ask the host engine for task relevant objects and use it. This is a way to communicate between nodes and tasks across the build.

You can opt-in to this behaviour.
Where you might want to do this:
 - When a task misbehaves and breaks the whole process
 - You have a task that is built in the same repo you're building your code and might change between runs. In this case we make sure that the DLLs are not locked at the end of the building. (Sandbox execution)

### SDK interaction
There are a few XML attributes or elements that will trigger SDK resolution, for example Project SDK=" " tag. When that happens we will go and ask the resolver who ca tell us where the SDK. There is also the top level element that has this effect. We can also add the import element so it specifies which file you're looking for.

### Cache
Special property or something that points to a dll that implements the cache interface. Like a plugin, so it would work out of a nuget package.
Task host cache (described more in the task host section (link))

#### Register Task Objects
If you are a tak and you need to meaningfully share state between invocations of said task in a single build or across builds the engine provides a mechanism to manage the lifetime of a .net object. When defining scope of objecdt's lifetime if can be per build or indefinetly.
This is available per node basis if you need to use it.
There is no serialization or desirialization, just a caching mechanism in memory.

#### Project result cache
(Are there toher types of cache?)
Cache keeps the results of built projects / tasks (project result cache). Cache is mostly on memory but can spill on disk.
RAR state cache has some custom on disk format.
Lives in the scheduler layer

Build results are stored as items 
The return and output attributes from a targets already serialize the results from said task. The result of a task is generally, sucess, failure and a list of items that succeeded.

#### Project result cache plugin
It was mainly use for distributed builds like CloudBuild.
Works as a middle layer between the scheduler and the cache, which allows more functionallity to be pluged in there. So scheduler asks t if they can satisfy node requests for project results.
Also adds profiles on disk, so it knows where to look for reults when asked.
We hash the inputs so we can create those profiles, and the plugin uses those to find the results and output of the request.
When it gets a hit, it downloads and copy the files to the right place, desirialize the the result payload and provide it to the engine.

#### Incremental build
All the output from the builds that are in memory becomes one big cache for MSBuild. The targets inputs and outputs that is what get's cached / saved. We don't really know how it works, oops.

### Scheduler
Something the schedules the execution of chunks of projects (targets or projects).
Scheduler maintains results of executed targets., as well as remembering which projects were already built and have their results. When a result is asked from a task / project that was built previously it gets it from the cache.
When communicating within the process. A task talks to the engine, which then communicates with the scheduler, to either get results or be blocked.

Build starts with an entry point. Scheduler assigns the entry point to a node (generally the in-proc node). Execution starts and targets start builds in some order. When an operation ends, they provide the results to the scheduler. The scheduler does not care about what the targets do, just what they return once executed.

It can also see pending requests. For example, when one task depends on another, in the middle of task execution, the node will tell the scheduler that it cannot complete, and it needs a list of requests of what it needs. The scheduler can either satisfy those requests from cache, or it puts it on the list for execution.

When a node is finished or blocked, the scheduler consider if they can assign more work to a specific node. And that is how out multiprocess stuff become parallelism.

#### MSBuild Task

We don't get it as a task but as an intrinsic method. Almost the same but different?

### Parallelism

Tasks run sequentially.

Parallelism is at the project level. Each project is single threaded until the process yields o MSBuild passes a task call.
MSBuild keeps control of the level of parallelism. Tasks however can implement parallelism indepedendt of projects.

For multi targeted builds, parallelism can be achieved but there are some extra steps. The outer build produces a list where the include of the list is the same project file each time, but different metadata for the target frameworks, and that is passed to the single MSBuild task so that it can build it in parallel.

#### Batching
Batching can be done in the targets and task levels.


*A lot of the talk here is what we could do, idk if it appl;ies too much right now*


### IPC (interprocess communication)
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
*Watch the Roman knowledge hand off to get more information about MSBuil server*

The MSBuild server is the idea of separating some processes. Splitting the entry point executable that lives for one build invocation, and the scheduler and the 1st in-proc node. This way you don't need to JIT and you can preserve your in-memory cache.

## MSBuild the programming language
This is not a general puporse language. Thefocus is to describe a project. So generally when it runs there is an output to files. Easier to express data rather than logic. We have properties and items -> data types (basically string, and string dictionary with a string attached).
Project file generally defines data, and what defined the actual process of the build is imported, either through the SDK, or though an explicit import elements that has libraries of tasks and targets that can be used to support building.
It has the resources for incremental builds and some performance improvments.

Has targets, targets can contain tasks. Tasks do arbitrary things, and there is an odering contraint when coding and executing them.

### Tasks
How we define this items within the MSBuild language.

In the task API there is a way that a task tells the engine it is blocked on results that depend on another task.
### Lifetimes

## MSBuild the API
It is a library of common build logic that defined basic things, like the convept of output folder, intermediate folder, so other tools can utilize these resources. 
It is also a way to manipulate the MSBuild Language without directly changing the language on a project itself.
It is an API focused on building .NET programs so there are some specific things.
 - Common targets: 
Core idea behind those targets is you're either manipulating MSBuild data or figuring out the intermediate. What things should be passed to the compiler, or you are invoking tools 
    - like command line aplications (exe). We also offer base class toolTask to provide an interface for that.

### ToolTask
We offer an interface called ToolTask where you can implement custom tasks to be processed during the build. This is more perfomant (on windows) than writing a script to do the same thing.

The engine will construct the task, call the execute method and then you can do whatever you want (was written in the task) during the execution
