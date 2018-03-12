# MSBuild binary log overview

Starting with MSBuild 15.3 a new binary log format is introduced, to complement the existing file and console loggers.

Goals:
 * completeness (more information than the most detailed file log)
 * build speed (doesn't slow the build down nearly as much as the diagnostic-level file log)
 * smaller disk size (10-20x more compact than a file log)
 * structure (preserves the exact build event args that can later be replayed to reconstruct the exact events and information as if a real build was running). File logs erase structure and are harder to parse (especially for multicore /m builds). Build analyzer tools are conceivable that could benefit from the structure in a binary log. An API is available to load and query binary logs.
 * optionally collect the project files (and all imported targets files) used during the build. This can help analyzing the logs and even view preprocessed source for all projects (with all imported projects inlined).

See http://msbuildlog.com for more information.

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
http://msbuildlog.com

# Binary log file format

The implementation of the binary logger is here:
https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BinaryLogger.cs
https://github.com/Microsoft/msbuild/blob/master/src/Build/Logging/BinaryLogger/BinaryLogger.cs

It is a `GZipStream`-compressed binary stream of serialized `BuildEventArgs` objects. The event args objects are serialized and deserialized using:
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsWriter.cs
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsReader.cs

## Incrementing the file format

Every .binlog file has the first three bytes that indicate the file version. The current file format version is 2 (`00 00 02`).

When incrementing the file format, keep this in mind:
 * Increment the version and add a summary of the changes: https://github.com/Microsoft/msbuild/blob/master/src/Build/Logging/BinaryLogger/BinaryLogger.cs#L22
 * In BuildEventArgsWriter.cs, just add fields, etc. without worrying. 
 * In BuildEventArgsReader.cs, add exactly the same changes, but wrapped in an `if`-statement like this: `if (fileFormatVersion > version where the field was introduced)
 * Open an issue over at https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new so I can adapt the Structured Log Viewer to these changes.

The format is backwards compatible, i.e. MSBuild will be able to play back .binlog files created with an older version of MSBuild. The Viewer will also be able to open files of any older version. Since the viewer updates automatically and I can push out updates easily, we can consider the Viewer is always able to read all .binlogs.

However MSBuild of version 15.3 won't be able to read .binlogs created with MSBuild version 15.6. This means the format is unfortunately not forwards-compatible. It is not self-describing, i.e. it doesn't carry its schema around for performance and compactness reasons. This is not a problem with a Viewer because Viewer is always up-to-date (there isn't an "old version" of the Viewer unless people go to great lengths to prevent it from auto-updating).