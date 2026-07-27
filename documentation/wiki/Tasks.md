# Tasks

A Task is a unit of execution in a Target and a method of extensibility in MSBuild.

## Basics

A task is a class implementing [`ITask`](https://github.com/dotnet/msbuild/blob/main/src/Framework/ITask.cs).

- The notable method of this interface is `bool Execute()`. Code in it will get executed when the task is run.
- A Task can have public properties that can be set by the user in the project file.
  - These properties can be:
    - **String types**: `string`, `string[]`
    - **Boolean types**: `bool`, `bool[]`
    - **Item types**: `ITaskItem` (representation of a file system object with metadata), `ITaskItem[]`
    - **Strongly-typed items**: `ITaskItem<T>` where `T` is `AbsolutePath`, `FileInfo`, or `DirectoryInfo` (e.g., `ITaskItem<AbsolutePath>`, `ITaskItem<FileInfo>`), and their array equivalents
    - **Path types**: `AbsolutePath`, `AbsolutePath[]`, `FileInfo`, `FileInfo[]`, `DirectoryInfo`, `DirectoryInfo[]`

    > The engine also accepts arbitrary value types (such as `int`, `DateTime`, or any custom struct) by converting to/from their string representation, but using them is discouraged. Prefer the documented types above.

    Guidance on choosing a type:
    - Use `string`/`string[]` only when the value is genuinely free-form text (a message, an identifier, a raw switch). If the value is a path, prefer one of the path types below so the engine roots it consistently and you avoid re-implementing path resolution in the task.
    - Use `AbsolutePath` when you need a fully-rooted path but do not need to touch the file system. The engine roots the incoming value against the project directory for you, and `AbsolutePath` is a lightweight struct that just carries the rooted string — pick it over `string` whenever a parameter is conceptually a path.
    - Use `FileInfo` when the parameter represents a single file and the task wants the `System.IO.FileInfo` conveniences (`Exists`, `Length`, `Directory`, `LastWriteTime`, etc.) without constructing the `FileInfo` itself. The value is rooted before the `FileInfo` is created, so relative inputs won't accidentally resolve against the process working directory.
    - Use `DirectoryInfo` for the directory equivalent of the above (a single directory parameter where the task wants `System.IO.DirectoryInfo` members). As with `FileInfo`, the path is rooted before construction.
    - Use `ITaskItem`/`ITaskItem[]` when the task needs item **metadata** (e.g. `Identity`, `RecursiveDir`, or custom metadata) in addition to the item spec.
    - Use `ITaskItem<T>` (e.g. `ITaskItem<AbsolutePath>`, `ITaskItem<FileInfo>`, `ITaskItem<DirectoryInfo>`) when you need **both** item metadata **and** the strongly-typed, pre-rooted value — the `Value` property exposes the parsed `T` while the item still carries its metadata. Prefer this over taking a bare `ITaskItem` and re-parsing `ItemSpec` yourself.
    - Prefer the scalar path types (`AbsolutePath`/`FileInfo`/`DirectoryInfo`) over `string` even for optional parameters; they make the task's intent explicit and centralize rooting in the engine.
  - The properties can have attributes `[Required]` which causes the engine to check that it has a value when the task is run and `[Output]` which exposes the property to be used again in XML

- Tasks have the `Log` property set by the engine to log messages/errors/warnings.

## Internals

- [`TaskRegistry`](https://github.com/dotnet/msbuild/blob/main/src/Build/Instance/TaskRegistry.cs) - has a list of all available tasks for the build, and resolves them.
- [`TaskExecutionHost`](https://github.com/dotnet/msbuild/tree/main/src/Build/BackEnd/TaskExecutionHost) - finds task in TaskRegistry, calls TaskFactory to create an instance of the task and sets its properties using reflection from the values in the XML. Then calls Execute on the task and gathers the outputs.
- TaskFactory - initializes the task, creates instance of the task
- ITask class - runs `Execute()` method

## Custom Tasks

Users can implement `ITask` and compile it into a .dll.
Then they can use in project file:

```xml
<UsingTask TaskName="MyTaskClass" AssemblyFile="MyTasks.dll"/>
```

This uses the AssemblyTaskFactory to load the task from the .dll and create an instance of it.

## Diagram of task lifecycle

```mermaid
graph

I["Implement:<br/>extend ITask interface in .dll"] --> R["Register:<br/>&lt;UsingTask /&gt;"] --> U["Use in XML:<br/>&lt;Target&gt;<br/>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&lt;MyTask /&gt;<br/>&lt;/Target&gt;"] --> In["Initialize:<br/> compile inline or load from assembly <br/>(TaskFactory)"] --> S["Setup:<br/> Set input properties<br/> (TaskExecutionHost)"] --> E["ITask.Execute()"] --> O["Gather outputs: <br/> (TaskExecutionHost)"]
```

## Task Factories

Task factories create instances of tasks. They implement [`ITaskFactory`](https://github.com/dotnet/msbuild/blob/main/src/Framework/ITaskFactory.cs) or [`ITaskFactory2`](https://github.com/dotnet/msbuild/blob/main/src/Framework/ITaskFactory2.cs).
This interface defines `bool Initialize(...)` and `ITask CreateTask(...)`.
They are e.g. responsible for loading a task from an assembly and initializing it.

The trait `MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC` forces inline tasks in an out of process TaskHost. It is not compatible with custom TaskFactories.

### Built-in Task Factories

- [`AssemblyTaskFactory`](https://github.com/dotnet/msbuild/blob/main/src/Build/Instance/TaskFactories/AssemblyTaskFactory.cs) - constructs tasks from .NET assemblies
- [`RoslynCodeTaskFactory`](https://github.com/dotnet/msbuild/blob/main/src/Tasks/RoslynCodeTaskFactory/RoslynCodeTaskFactory.cs) - inline code tasks
- CodeTaskFactory, XamlTaskFactory - old, rarely used

### Custom Task Factories

This is a rarely used method of extensibility.
Users can implement `ITaskFactory` to create custom task factories.
Then they can use in project file:

```xml
<UsingTask TaskName="MyTask" AssemblyFile="Factory.dll" Factory="MyTaskFactory">
    <Task>Insides that the MyTaskFactory uses to initialize</Task>
</UsingTask>
```

## Microsoft Learn Resources

- [MSBuild task](https://learn.microsoft.com/visualstudio/msbuild/msbuild-task)
- [Task reference](https://learn.microsoft.com/visualstudio/msbuild/msbuild-task-reference)
- [Task Writing](https://learn.microsoft.com/visualstudio/msbuild/task-writing)
- [Creating custom task tutorial](https://learn.microsoft.com/visualstudio/msbuild/tutorial-custom-task-code-generation)
