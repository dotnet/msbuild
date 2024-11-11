## Logging behaviour
MSBuild has a few different built-in loggers, which have different behaviours depending on verbosity. For more information on loggers you can visit the [Microsoft Learn page](https://learn.microsoft.com/visualstudio/msbuild/obtaining-build-logs-with-msbuild), or take a look at the [high-level overview of MSBuild](https://github.com/dotnet/msbuild/blob/main/documentation/High-level-overview.md#diagnosability--loggers).

If you are expecting to see a certain type of message (like test logs) but are unable to find it in one of our loggers, check if the verbosity is correct and if the message has the correct type and importance.

### Message types
There are various types of messages within MSBuild with different importances and purposes.
There are some message types that are built-in within the engine, `errors`, `warnings`, and MSBuild engine information. The custom messages, that can come either from the engine or other sources, are selected and displayed based on the `importance` of the message. There can be high, normal, and low importance messages being displayed. More detail on which messages are displayed on individual loggers are on their respective sections.

For more information on custom messages you can more ou Microsoft Learn page for the [MSBuild Message Task](https://learn.microsoft.com/visualstudio/msbuild/message-task)

### Console logger
Console logger refers to the logger that outputs to the console in VS or the terminal that is being used. It is not the default logger after the [.NET 9 update](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/9.0/terminal-logger) but still can be accessed by opting out of the Terminal Logger feature.

The console logger defaults to normal verbosity, and can be overriden by passing the `-verbosity` attribute, or passing the `verbosity` property to the console logger `clp:verbosity={verbosity level}`.

| Verbosity                  | Quiet | Minimal | Normal | Detailed | Diagnostic |
| ---------                  | ----- | ------- | ------ | -------- | ---------- |
| Errors                     |&check;| &check; | &check;| &check;  | &check;    |
| Warnings                   |&check;| &check; | &check;| &check;  | &check;    |
| High-importance messages   |       | &check; | &check;| &check;  | &check;    |
| Normal-importance messages |       |         | &check;| &check;  | &check;    |
| Low-importance messages    |       |         |        | &check;  | &check;    |
| MSBuild engine information |       |         |        |          |            |

### Terminal logger
Terminal logger is a new feature which improves the console experience. 
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

### File logger
The File logger saves all the build data to a file. It's verbosity is determined by passing the `verbosity` parameter to the `flp` attribute, or the default is set to `diagnostic`, and it follows the same message display rules as the console logger.

### Binary logger / build logger
The binary logger is a bit different as it does not have a verbosity option. It includes all messages, projects and files from the build by default.
You can find more information about the binlogs on [MSBuild Github Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md).
