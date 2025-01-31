# MSBuild binary log overview

Starting with MSBuild 15.3 a new binary log format is introduced, to complement the existing file and console loggers.

Goals:
 * completeness (more information than the most detailed file log)
 * build speed (doesn't slow the build down nearly as much as the diagnostic-level file log)
 * smaller disk size (10-20x more compact than a file log)
 * structure (preserves the exact build event args that can later be replayed to reconstruct the exact events and information as if a real build was running). File logs erase structure and are harder to parse (especially for multicore /m builds). Build analyzer tools are conceivable that could benefit from the structure in a binary log. An API is available to load and query binary logs.
 * optionally collect the project files (and all imported targets files) used during the build. This can help analyzing the logs and even view preprocessed source for all projects (with all imported projects inlined).

See https://msbuildlog.com/ for more information.

# Creating a binary log during a build

Use the new `/bl` switch to enable the binary logger:
```
> msbuild.exe MySolution.sln /bl
```

By default the binary log file is named `msbuild.binlog` and it is written to the current directory. To specify a custom log file name and/or path, pass it after a colon:
```
> msbuild.exe MySolution.sln /bl:out.binlog
```

You can use the binary logger simultaneously with other loggers, such as text file (/fl) and console loggers. They are independent and having a binary log side-by-side with other logs may be beneficial (for sending a log to other people or running automatic build analysis tools that rely on the exact build event structure without having to parse text logs).

When using the binary logger all other log formats are technically redundant since you can later reconstruct all the other logs from the binary log. To turn off console logging, pass the `/noconlog` switch. Builds will usually be much faster if you don't pass the console and file loggers.

# Collecting projects and imports source files

By default the binary logger will collect the source code of all project files and all imported project/targets files used during the build. You can control this behavior:
 * `/bl:ProjectImports=None` (do not collect project and imports files)
 * `/bl:ProjectImports=Embed` (default - embed in the `.binlog` file)
 * `/bl:ProjectImports=ZipFile` (produce a separate `.ProjectImports.zip` file next to the log file that contains the files)

Note that only `*.csproj`, `*.targets` and other MSBuild project formats are collected. No other source files (`*.cs`, `*.cpp` etc) are collected.

If the binary log contains the projects/imports files the MSBuild Structured Log Viewer will display all the files contained in the log, let you search through them and even display preprocessed view for any project where all imported projects are inlined (similar to `msbuild /pp` switch).

# Logging all environment variables

By default, MSBuild logs only the environment variables that are used to influence MSBuild, which is a subset of what is set in the environment. This reduces, but does not eliminate, the likelihood of leaking sensitive information through logs. This behavior can be changed to log the full environment by setting the environment variable `MSBUILDLOGALLENVIRONMENTVARIABLES=1`.

# Replaying a binary log

Instead of passing the project/solution to MSBuild.exe you can now pass a binary log to "build". This will replay all events to all other loggers (just the console by default). Here's an example of replaying a `.binlog` file to the diagnostic verbosity text log:

```
> msbuild.exe msbuild.binlog /noconlog /flp:v=diag;logfile=diag.log
```

# Creating a binary log with older MSBuild versions

It is also possible to use the BinaryLogger with older MSBuild versions, such as MSBuild 14.0. For this you'll need the StructuredLogger.dll available here:
https://github.com/KirillOsenkov/MSBuildStructuredLog/releases/download/v1.0.130/StructuredLogger.dll

Alternatively you can download/install the https://www.nuget.org/packages/Microsoft.Build.Logging.StructuredLogger NuGet package and use the `StructuredLogger.dll` provided by it.

Once you have the `StructuredLogger.dll` on disk you can pass it to MSBuild like this:

```
> msbuild.exe /logger:BinaryLogger,"path\to\StructuredLogger.dll";msbuild.binlog
```

# Using MSBuild Structured Log Viewer

You can use the MSBuild Structured Log Viewer tool to view `.binlog` files:
https://msbuildlog.com/

# Collecting binary logs from Visual Studio builds

[see more details](Providing-Binary-Logs.md#capturing-binary-logs-through-visual-studio)

# Binary log file format

The implementation of the binary logger is here:
https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BinaryLogger.cs
https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BinaryLogger.cs

It is a `GZipStream`-compressed binary stream of serialized `BuildEventArgs` objects. The event args objects are serialized and deserialized using:
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsWriter.cs
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsReader.cs

## Meaning of various Ids in the BuildEventArgs

The [`BuildEventArgs`](https://github.com/dotnet/msbuild/blob/main/src/Framework/BuildEventArgs.cs) sent to the loggers (and later stored in the binlog) can have [`BuildEventContext`](https://github.com/dotnet/msbuild/blob/main/src/Framework/BuildEventContext.cs) attached. This context contains multiple integer Ids, that can be of interest for the consumer:
* `ProjectInstanceId` - This indicates unique combination of a project and global properties (basically a project configuration for a build.). The same combination dictates a need for evaluation (or possibility to reuse existing) - so the id correlates with `EvaluationId`. `ProjectInstanceId` is however not present on evaluation events.
* `EvaluationId` - Indicates unique evaluation run - that needs to happen for each unique combination of project and global properties. `EvaluationId` is present on all evaluation time events and on the `ProjectStartedEventArgs` (this event can be used to correlate the `EvaluationId` with `ProjectInstanceId` - to get all build execution time events that used a specific evaluation).
* `ProjectContextId` - This indicates unique build request (so request for result from project + target(s) combination). There can be multiple build requests using the same evaluation - so a single `ProjectInstanceId` (and `EvaluationId`) often maps to multiple `ProjectContextId`s
* `NodeId` - indicates the node where the event was generated ('0' for the SchedulerNode with possible in-proc execution node, positive ids for the out-of-proc execution nodes). The whole evaluation happens on a single node - so all evaluation time events with single `EvaluationId` have same `NodeId`. Execution is attempted to be performed on a node which evaluated ('evaluation affinity') - so usually all events with corresponding `EvaluationId` and `InstanceId` have the same `NodeId`. But evaluation results are transferable between nodes (it's `Translatable`) so evaluation events and build events `NodeId` doesn't have to match. Single build execution happens on the same node - so all events with same `ProjectContextId` have same `NodeId`. Though multiple build executions can be interleaved on a same node (due to 'Yielding' - either voluntarily explicitly called by the Task, or implicitly enforced by `RequestBuilder`).

```
# Project.csproj
└── EvaluationId: ABC                   # Single evaluation of the project
   └── ProjectInstanceId: XYZ           # Single instance created from evaluation
       ├── ProjectContextId: 123        # Build request for Compile target  
       └── ProjectContextId: 456        # Build request for Pack target
```
In this example:

* The project is evaluated once, generating `EvaluationId`: ABC
* This evaluation creates one project instance with `ProjectInstanceId`: XYZ
* Two separate build requests are made:
    - One to build the Compile target (`ProjectContextId`: 123)
    - One to build the Pack target (`ProjectContextId`: 456)

It's also good to note that those Ids can have negative values - indicating uninitialized value (this can be expected in many cases - e.g. evaluation time events cannot have `ProjectContextId` as they are not tied to single result request; or `ProjectInstanceId` are not ever populated on evaluation time events).

## Incrementing the file format

Every .binlog file has the first four bytes that indicate the file version. The current file format is indicated in [`BinaryLogger.cs`](/src/Build/Logging/BinaryLogger/BinaryLogger.cs).

When incrementing the file format, keep this in mind:
 * Increment the version and add a summary of the changes: https://github.com/dotnet/msbuild/blob/main/src/Build/Logging/BinaryLogger/BinaryLogger.cs#L22
 * In BuildEventArgsWriter.cs, just add fields, etc. without worrying. 
 * In BuildEventArgsReader.cs, add exactly the same changes, but wrapped in an `if`-statement like this: `if (fileFormatVersion > version where the field was introduced)
 * Open an issue over at https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new so I can adapt the Structured Log Viewer to these changes.

The format is backwards compatible, i.e. MSBuild will be able to play back .binlog files created with an older version of MSBuild. The Viewer will also be able to open files of any older version. Since the viewer updates automatically and I can push out updates easily, we can consider the Viewer is always able to read all .binlogs.

## Forward compatibility reading

From version 18, the binlog contains as well the minimum version of reader that can interpret it (stored in bytes 4 to 8). Support for best effort forward compatibility is added by this version. It is “best effort” only because the binlog format is not self-describing, i.e. it doesn't carry its schema around for performance and compactness reasons.

This is not of a high importance for users of the Viewer because Viewer is always up-to-date (there isn't an "old version" of the Viewer unless people go to great lengths to prevent it from auto-updating).

## Reading API

We recommend usage of `BinaryLogReplayEventSource`. It provides simplified helpers for creating and configuring `BuildEventArgsReader` and subscribing to the events.

```csharp
var logReader = new BinaryLogReplayEventSource()
{
    AllowForwardCompatibility = true
};

// Handling of the structured events contained within the log
logReader.AnyEventRaised += (_, e) =>
{
    if (e is BuildErrorEventArgs error)
    {
        //...
    }

    // ...
};

// Starts the synchronous log reading loop.
logReader.Replay(path_to_binlog_file);

```

### Handling the recoverable reading errors

In compatibility mode (default for `BinaryLogReplayEventSource`. Only supported for binlogs of version 18 and higher) reader is capable of skipping unknown event types and unknown parts of known events (`BuildEventArgsReader` can configure the behavior via 2 separate properties - `SkipUnknownEvents` and `SkipUnknownEventParts`).

The unknown events and event parts are regarded as recoverable errors, since the reader is able to continue reading subsequent records in the binlog. However the specific user logic should have the last call in deciding whether errors are really recoverable (e.g. is presence of unrecognized or unparseable event ok? It might be fine when searching only for specific events - e.g. errors but not acceptable when trying to provide definitive overview of the built).

To allow the calling code to decide - based on the type of error, type of events getting the error, or the number of errors - the `RecoverableReadError` event is exposed (from both `BinaryLogReplayEventSource` and `BuildEventArgsReader`).

```csharp
/// <summary>
/// An event args for <see cref="IBinaryLogReaderErrors.RecoverableReadError"/> event.
/// </summary>
public sealed class BinaryLogReaderErrorEventArgs : EventArgs
{
    /// <summary>
    /// Type of the error that occurred during reading.
    /// </summary>
    public ReaderErrorType ErrorType { get; }

    /// <summary>
    /// Kind of the record that encountered the error.
    /// </summary>
    public BinaryLogRecordKind RecordKind { get; }

    /// <summary>
    /// Materializes the error message.
    /// Until it's called the error message is not materialized and no string allocations are made.
    /// </summary>
    /// <returns>The error message.</returns>
    public string GetFormattedMessage() => _formatErrorMessage();
}

/// <summary>
/// Receives recoverable errors during reading.
/// Communicates type of the error, kind of the record that encountered the error and the message detailing the error.
/// In case of <see cref="ReaderErrorType.UnknownEventData"/> this is raised before returning the structured representation of a build event
/// that has some extra unknown data in the binlog. In case of other error types this event is raised and the offending build event is skipped and not returned.
/// </summary>
event Action<BinaryLogReaderErrorEventArgs>? RecoverableReadError;
```

Our sample usage of the [Reading API](#reading-api) can be enhanced with recoverable errors handling e.g. as such:

```csharp

// Those can be raised only during forward compatibility reading mode.
logReader.RecoverableReadError += errorEventArgs =>
{
    // ...

    // e.g. we can decide to ignore the error and continue reading or break reading
    //  based on the type of the error or/and type of the record or/and the frequency of the error

    // Would we decide to completely ignore some errors - we can aid better performance by not materializing the actual error message.
    // Otherwise the error message can be materialized via the provided method on the event argument:
    Console.WriteLine($"Recoverable reader error: {errorEventArgs.GetFormattedMessage()}");
};

```

When authoring changes to the specific BuildEventArg types - it is always strongly recommended to **prefer append-only changes**. 

This prevents the possibility of collision where some fields are removed in one version and then different fields with same binary size are added in future version. Such a sequence of format changes might not be caught by the decoder and might lead to unnoticed corrupt interpretation of data. For this reason the author of specific OM changes should always check whether there is a possibility of unrecognizable format collision (same binary size, different representation) within binlog versions of a same [minimum reader version support](#forward-compatibility-reading). If this is possible, the [minimum reader version support](#forward-compatibility-reading) should be incremented.
