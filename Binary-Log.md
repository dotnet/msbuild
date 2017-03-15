# MSBuild binary log overview

Starting with MSBuild 15.3 a new binary log format is introduced, to complement the existing file and console loggers.

Goals:
 * completeness (more information than the most detailed file log)
 * build speed (doesn't slow the build down nearly as much as the diagnostic-level file log)
 * smaller disk size (10-20x more compact than a file log)
 * structure (preserves the exact build event args that can later be replayed to reconstruct the exact events and information as if a real build was running). File logs erase structure and are harder to parse (especially for multicore /m builds).

# Creating a binary log during a build

Use the new `/bl` switch to enable the binary logger:
```
> msbuild.exe MySolution.sln /bl
```

By default the binary log file is `msbuild.binlog` and it's written to the current directory. To specify a custom log file name and/or path, pass it after a colon:
```
> msbuild.exe MySolution.sln /bl:out.binlog
```

When using the binary logger all other log formats are technically redundant since you can later reconstruct all the other logs from the binary log. To turn off console logging, pass the `/noconlog` switch.

# Creating a binary log with older MSBuild versions

It is also possible to use the BinaryLogger with older MSBuild versions, such as MSBuild 14.0. You'll need to download the https://www.nuget.org/packages/Microsoft.Build.Logging.StructuredLogger NuGet package and save the StructuredLogger.dll somewhere. Then pass it to MSBuild like this:

```
> msbuild.exe /logger:BinaryLogger,"path\to\StructuredLogger.dll";msbuild.binlog
```

# Replaying a binary log

Instead of passing the project/solution to MSBuild.exe you can now pass a binary log to "build". This will replay all events to all other loggers (just the console by default). Here's an example of replaying a `.binlog` file to the diagnostic verbosity text log:

```
> msbuild.exe msbuild.binlog /noconlog /flp:v=diag;logfile=diag.log
```

# Using MSBuild Structured Log Viewer

You can use the MSBuild Structured Log Viewer tool to view `.binlog` files:
https://github.com/KirillOsenkov/MSBuildStructuredLog

# Binary log file format

The implementation of the binary logger is here:
https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BinaryLogger.cs
https://github.com/Microsoft/msbuild/blob/master/src/Build/Logging/BinaryLogger/BinaryLogger.cs

It is a `GZipStream`-compressed binary stream of serialized `BuildEventArgs` objects. The event args objects are serialized and deserialized using:
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsWriter.cs
 * https://source.dot.net/#Microsoft.Build/Logging/BinaryLogger/BuildEventArgsReader.cs