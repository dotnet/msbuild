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

You can embed additional arbitrary files in the binary log by adding them to the `EmbedInBinlog` item. Prefer absolute paths: a relative `Include` path is resolved against the directory of the project that declares the item, so that items emitted by child projects in a multi-project build are captured correctly.

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

Alternatively you can download/install the https://www.nuget.org/packages/MSBuild.StructuredLogger NuGet package and use the `StructuredLogger.dll` provided by it.

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

<a id="filtering-events-during-replay"></a>

### Filtering events during builds and replay

Add `Exclude=kind[,kind...]` to a binary logger's parameters to omit selected event kinds.
The same implementation filters events before serialization during an ordinary build
and when rewriting an existing binlog. The option affects only that binary logger:
console output, file loggers, and other binary loggers still receive their own events.
Excluding errors or warnings does not change the build result.

#### Produce filtered binlogs from the command line

Use an MSBuild build containing the `Exclude` binary logger parameter. Until this change
is released, use MSBuild built from this branch; older installed SDKs do not recognize it.
For example, filter the original build's log while keeping normal console diagnostics:

```powershell
dotnet build "C:\src\App\App.csproj" "-bl:C:\logs\filtered.binlog;Exclude=Message,Warning;ProjectImports=None"
```

Use the same parameter when rewriting an existing binlog:

```powershell
dotnet msbuild "C:\logs\input.binlog" "-bl:C:\logs\filtered.binlog;Exclude=ProjectEvaluationStarted,ProjectEvaluationFinished;ProjectImports=None" -noAutoResponse
```

The same arguments work with `MSBuild.exe`. Passing a binlog replays it; it does not rebuild
the original project. Multiple `-bl` arguments can have independent filters, or no filter:

```powershell
dotnet msbuild "C:\logs\input.binlog" "-bl:C:\logs\quiet.binlog;Exclude=Message" "-bl:C:\logs\full.binlog" -noAutoResponse
```

During filtered replay, all binary log destinations must be new paths, including the
default `msbuild.binlog` when only `-bl:Exclude=...` is specified. Ordinary builds retain
the binary logger's normal overwrite behavior. The usual `{}` output-name expansion is
supported. Relative output paths are resolved from the current directory, including
directories whose names contain semicolons.

Specify `Exclude=` once per logger, including parameters from response files. `Exclude=`
and event kind names are case-insensitive. Separate names with commas; duplicate names
within the list are harmless. Empty lists, numeric values, unknown names, auxiliary
records, and protected event kinds are rejected.

Selection matches exact serialized record kinds, not event-class inheritance. For
example, `Exclude=Message` does not exclude `CriticalBuildMessage` or `TaskCommandLine`;
name those kinds explicitly to exclude them too.

| Excludable group | Event kinds |
| --- | --- |
| Diagnostics | `Error`, `Warning`, `Message`, `CriticalBuildMessage` |
| Task diagnostics | `TaskCommandLine`, `TaskParameter` |
| Evaluation | `ProjectEvaluationStarted`, `ProjectEvaluationFinished` (exclude both or neither) |
| Imports and properties | `ProjectImported`, `PropertyReassignment`, `UninitializedPropertyRead`, `EnvironmentVariableRead`, `PropertyInitialValueSet` |
| Other diagnostics | `ResponseFileUsed`, `AssemblyLoad` |
| Recorded BuildCheck diagnostics | `BuildCheckMessage`, `BuildCheckWarning`, `BuildCheckError`, `BuildCheckTracing`, `BuildCheckAcquisition` |

Build, project, target, and task start/finish events, `TargetSkipped`, and other lifecycle
and correlation records are protected. These restrictions retain the structure needed
by the ordinary console logger; they do not guarantee that every consumer can perform
the same analyses after data is excluded.

Filtered replay uses the ordinary console logger, not the terminal logger. Its console
output is not filtered. Besides binary, custom, file, and distributed loggers, supported
switches are verbosity (`-v`), console logger parameters
(`-clp`), `-nologo`, `-m`, `-nr`, `-lowPriority`, `-noAutoResponse`, `-noConsoleLogger`,
and automatic or disabled terminal selection (`-tl:auto` or `-tl:false`, both using the
ordinary console logger here). Build-only switches and `-check` are rejected before their
outputs are opened. The build logger automatically registered by `dotnet msbuild` from
the current SDK is ignored. Terminal logger parameters (`-tlp`), including
those supplied by the SDK, do not configure the ordinary console logger.
Use `-noAutoResponse`, as above, to avoid
inheriting build-only switches from automatic response files.

`ProjectImports=Embed` remains the default; `ProjectImports=None` omits the archive.
`ProjectImports=ZipFile` is supported during builds, but not during filtered replay because
its sidecar archive is not part of the staged binary log publication.
Archives are handled independently of event selection: excluding `ProjectImported` or
evaluation events does not remove embedded source files. `Exclude=Message` also excludes
the binary logger's own metadata messages; `OmitInitialInfo` can suppress initial metadata
without excluding other messages. **Filtering is not redaction**, and does not guarantee
a smaller output.

For CLI filtered replay, the input must use a format supported by this reader, no newer
than its current format.
Unlike ordinary forward-compatible replay, filtered rewriting does not skip unknown
records or fields. It writes the current binlog format, not a byte-for-byte copy.

The CLI stages each binary output in its destination directory and publishes it without
overwriting only after replay and logger finalization succeed. Cancellation observed
before publication begins, read/write failures, and finalization/publication failures
produce a nonzero exit status.
Existing destinations, including files created while replay is running, are preserved.
Publication is atomic per file, not across all outputs: a later publication failure can
leave earlier successfully published binlogs. Other logger outputs are not staged.
Temporary-file cleanup failures are reported. Exit status describes the transformation,
not whether the build recorded in the input succeeded.

For programmatic per-logger filtering, set `BinaryLogger.Parameters` to include `Exclude=...`
and initialize it with either a live build event source or a `BinaryLogReplayEventSource`.
The logger forces structured replay so raw passthrough cannot bypass its filter. Events
are still deserialized and delivered to other subscribers; use the replay-source API below
when the same predicate should apply to every subscriber and skip rejected payloads early.

Direct `BinaryLogger` use does not provide the CLI's staging and no-overwrite guarantees.
Choose distinct input and output paths, and manage output publication and failure cleanup
in the calling application. `BinaryLogger.Initialize` normally creates or overwrites its
configured file; the overload taking an output stream writes to that stream instead.

#### Run a filtered replay from a console application

Set `BinaryLogReplayEventSource.EventFilter` when constructing the replay source to select
which events reach subscribers. The callback receives `BinaryLogEventMetadata`, containing
the record kind, its `BuildEventContext` (if present), and the original build context for a
`TargetSkipped` event. Return `true` to retain an event or `false` to skip it. A `null` filter
leaves replay unchanged. All `Replay` overloads apply the filter.

The following API example prints only errors and warnings from an existing binlog without
rebuilding the original project or changing the input file.

Use a .NET SDK supported by the `Microsoft.Build` package version you are referencing.
Create a console application and add the package, replacing `VERSION_WITH_EVENT_FILTER`
with a version containing this API. Older packages without `EventFilter` cannot compile
this example. To try an unreleased build of this change, configure a local NuGet source
containing the packages built from this branch.

```powershell
dotnet new console --name FilterBinlog --no-restore
dotnet add .\FilterBinlog\FilterBinlog.csproj package Microsoft.Build --version "VERSION_WITH_EVENT_FILTER" --no-restore
```

Replace `FilterBinlog\Program.cs` with:

```csharp
using System;
using Microsoft.Build.Logging;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: FilterBinlog <path-to-binlog>");
    return 1;
}

var logReader = new BinaryLogReplayEventSource
{
    EventFilter = metadata =>
        metadata.RecordKind is BinaryLogRecordKind.Error or BinaryLogRecordKind.Warning
};

logReader.AnyEventRaised += (_, e) => Console.WriteLine(e.Message);
logReader.Replay(args[0]);
return 0;
```

Build the sample, then invoke it with the path to your binlog:

```powershell
dotnet build .\FilterBinlog\FilterBinlog.csproj -bl:{{}}
dotnet run --project .\FilterBinlog\FilterBinlog.csproj --no-build -- "C:\logs\build.binlog"
```

The `--` separates the application's argument from `dotnet run` options. Replay runs
synchronously and prints each retained event's message. If the log contains no errors
or warnings, the application prints nothing and exits successfully. To select different
events, change the `EventFilter` predicate; event subscribers receive only accepted events.

This example selects diagnostics for display, not for creating a complete build log.
When writing a filtered binlog or passing events to a consumer that relies on build
structure, retain a consistent set of events (for example, matching start/finish events
and any required parent project or target events).

To write accepted events to a caller-provided stream, configure a `BinaryLogger`'s
`Parameters`, then call `binaryLogger.Initialize(logReader, outputStream)`.
Call `Shutdown()` to finalize the log and close the stream; dispose the stream yourself
if initialization fails. The configured file path remains the log's metadata name and
controls import archive paths, but the binlog itself is written to the supplied stream.

#### Filtering behavior and failures

The replay source's `EventFilter` forces structured reading instead of raw record
passthrough. With length-framed logs (format version 18 or later), rejected events skip
their type-specific payload without deserialization. `TargetSkipped` is an exception: its original build context
requires deserializing the payload first. Older formats also filter after deserialization.
Auxiliary records, including strings, name/value lists, and embedded content, are still
read so retained events can be decoded correctly. Cancellation is checked between rejected
records too, but cannot interrupt a callback already running.

Unknown record kinds are not offered to the filter or interpreted as known event payloads.
They follow the existing forward-compatibility policy: when recovery is enabled for a
newer format, they are skipped with a `RecoverableReadError` notification; strict reading
still rejects them.

If a filter throws, replay stops with `BinaryLogEventFilterException`. This is a callback
failure, not a recoverable log-format error: `AllowForwardCompatibility` does not suppress
it and `RecoverableReadError` is not raised for it. The exception provides:

| Property | Diagnostic information |
| --- | --- |
| `InnerException` | The original exception, including its type, message, stack trace, and any nested exceptions. |
| `RecordKind` | The kind of event passed to the failing callback. |
| `BuildEventContext` | The event's build context, or `null` if absent. |
| `OriginalBuildEventContext` | The original context of a `TargetSkipped` event, or `null` if absent. |
| `RecordNumber` | The zero-based record number in the reader, including auxiliary and rejected records; not a byte offset or count of dispatched events. |
| `FileFormatVersion` | The format version of the source binary log. |

The localized exception message includes the record kind, number, format version, and
available contexts. The properties remain available across .NET Framework exception
serialization. When manually constructing the exception with only an inner exception,
the record properties are `null` because no record information was supplied.

To log a callback failure explicitly, replace the `logReader.Replay(args[0])` call in
the console application above with:

```csharp
try
{
    logReader.Replay(args[0]);
}
catch (BinaryLogEventFilterException ex)
{
    Console.Error.WriteLine(ex); // Includes record information and the original exception.
    throw;
}
```

Low-level readers can use `BuildEventArgsReader.Read(eventFilter)` directly; it returns
the next accepted event and reports filter failures with the same exception information.

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
