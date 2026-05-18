## Logging behavior
MSBuild has a few different built-in loggers, which have different behaviors depending on verbosity. For more information on loggers you can visit the [Microsoft Learn page](https://learn.microsoft.com/visualstudio/msbuild/obtaining-build-logs-with-msbuild), or take a look at the [high-level overview of MSBuild](https://github.com/dotnet/msbuild/blob/main/documentation/High-level-overview.md#diagnosability--loggers).

If you are expecting to see a certain type of message (like test logs) but are unable to find it in one of our loggers, check if the verbosity is correct and if the message has the correct type and importance.

### Message types
There are various types of messages within MSBuild with different importances and purposes.
Some message types are built-in within the engine, such as  `errors`, `warnings`, and MSBuild engine information. Others are custom messages, that can come either from the engine or other sources, and are selected and displayed based on the `importance` of the message. There can be high, normal, and low importance messages being displayed. More detail on which messages are displayed on individual loggers are on their respective sections.

For more information on custom messages please reference the Microsoft Learn page for the MSBuild [Message](https://learn.microsoft.com/visualstudio/msbuild/message-task) Task.

### Terminal logger
Terminal logger is a new feature which improves the console experience for end users by focusing the output on the diagnostics raised from a build for each project. It also allows users to see at-a-glance information about how the engine is building their projects at any time. It is opinionated and explicitly hides some build messages and output to deliver a more streamlined end-user experience. Users that need more detailed output should use the [console logger](#console-logger) or a [binary log](#binary-logger-build-logger) along with the [Structured Log Viewer](https://msbuildlog.com/) to drive their investigations.
For more information on how the terminal logger behaves see the [dotnet build options](https://learn.microsoft.com/dotnet/core/tools/dotnet-build#options) under `-tl`.

To specify verbosity the `-verbosity` flag or `/tlp:verbosity={verbosity level}`

| Verbosity                  | Quiet | Minimal | Normal | Detailed | Diagnostic |
| ---------                  | ----- | ------- | ------ | -------- | ---------- |
| Errors                     |&check;| &check; | &check;| &check;  |   &check;  |
| Warnings                   |&check;| &check; | &check;| &check;  |   &check;  |
| High-importance messages   |       |         |        | &check;  |   &check;  |
| Normal-importance messages |
| Low-importance messages    |
| MSBuild engine information |

### Binary logger / build logger
The binary logger does not have a verbosity option. It includes all messages, projects and files from the build by default. It is intended to be a tooling-friendly way to get detailed information about what happened during a build, for analysis or automated processing.

You can find more information about the binlogs on [MSBuild Github Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md).

### Console logger
Console logger refers to the logger that outputs to the console in VS or the terminal that is being used. It is not the default logger after the [.NET 9 update](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/9.0/terminal-logger) but still can be accessed by opting out of the Terminal Logger feature.

The console logger is a 1:1 textual representation of the data that are emitted during the build. It attempts small amounts of formatting, but it writes received messages from all of the worker nodes in an unbuffered format so can be difficult to follow the chain of execution.

The console logger defaults to normal verbosity, and can be overriden by passing the `-verbosity` attribute, or passing the `verbosity` property to the console logger `clp:verbosity={verbosity level}`.

| Verbosity                  | Quiet | Minimal | Normal | Detailed | Diagnostic |
| ---------                  | ----- | ------- | ------ | -------- | ---------- |
| Errors                     |&check;| &check; | &check;| &check;  | &check;    |
| Warnings                   |&check;| &check; | &check;| &check;  | &check;    |
| High-importance messages   |       | &check; | &check;| &check;  | &check;    |
| Normal-importance messages |       |         | &check;| &check;  | &check;    |
| Low-importance messages    |       |         |        | &check;  | &check;    |
| MSBuild engine information |       |         |        |          |            |

### File logger
The File logger saves all the build data to a file. It's verbosity is determined by passing the `verbosity` parameter to the `flp` attribute, or the default is set to `diagnostic`, and it follows the same message display rules as the console logger.

