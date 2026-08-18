# MSBuild data-collection systems

MSBuild has three main logging and feedback systems, with different characteristics and audiences. This is an overview focused on the purposes of the systems as relevant to developers of MSBuild.

## MSBuild loggers

Loggers are the primary user-facing way to understand what MSBuild is doing. It's possible to write a custom logger, but most people use the [built-in ones](Logging-behavior.md):

* `TerminalLogger` for interactive console use in .NET SDK,
* `BinaryLogger` for detailed capture and analysis,
* `ConsoleLogger`, the default MSBuild output when redirected and in `MSBuild.exe`, and
* `FileLogger`, the longstanding “more detailed than console” text output.

Mechanically, loggers receive logging events and note their details, serializing them in their entirety in the case of the binlog and ignoring or textualizing them for the other primary loggers.

New `*EventArgs` classes should carry most of their information via their _structure_, for easier analysis. Many older events render their information to a string that can be difficult to parse.

Logs can be and often are analyzed post-build. Interesting use cases are

* The Visual Studio Code or GitHub Actions “problem matcher” regexes that take text output of the build and present it as “build errors”.
* The [Structured Log Viewer](https://msbuildlog.com) application (for interactive analysis).
* Tools built on top of the binlog APIs.

## Tracing events

MSBuild also emits [trace events](specs/event-source.md). These are structured events (ETW on Windows) that can be captured via runtime configuration outside of MSBuild. They contain less information than a full binlog, but have lower performance impact to enable.

These events are captured by default in Visual Studio performance infrastructure, including “Speedometer” and “Perf DDRIT” tests and Visual Studio Feedback when users “record a trace” of the problem. They can also be manually collected and analyzed in PerfView.

## Telemetry

MSBuild can also be configured to send [telemetry](VS-Telemetry-Data.md) through the Visual Studio and .NET SDK channels. This allows getting aggregate data from a variety of users, but we have to consider data volume and privacy.
