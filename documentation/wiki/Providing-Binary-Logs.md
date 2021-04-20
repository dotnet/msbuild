# Providing MSBuild Binary Logs for investigation

MSBuild has the ability to capture a detailed binary log file.  If you are having a build issue and are able to provide a binary log, this can be very helpful for investigating the issue.

However, you should be aware what type of information is captured in the binary log to make sure you are not inadvertently sharing more than you intend.  The binary log captures almost everything your build does, including the contents of your project files and any files (such as .props and .targets) that they import, all tasks that are run during the build as well as the input and output, as well as all environment variables.  It generally doesn't include the contents of the source files that are compiled, but it does capture their full names and paths.

⚠ NOTE: some build environments make secrets available using environment variables. Before sharing a binary log, make sure it does not expose API tokens or other important secrets.

You can create a binary log by passing the `-bl` parameter to MSBuild. You can explore the contents of the generated .binlog file using [MSBuild Structured Log Viewer](http://msbuildlog.com/) or in your browser using [Live Structured Log Viewer](https://live.msbuildlog.com). Note: We don't capture any data from binary logs viewed on your browser.

[More details about binary logs](Binary-Log.md)

## Capturing Binary Logs Through Visual Studio
See [this guide](https://github.com/dotnet/project-system-tools) in the Project System Tools repo for capturing binlogs through Visual Studio.