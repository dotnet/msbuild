// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.CommandLine
{
    internal interface ICommandLineSwitches
    {
        // Parameterless switches
        bool? Help { get; }
        bool? Version { get; }
        bool? NoLogo { get; }
        bool? NoAutoResponse { get; }
        bool? NoConsoleLogger { get; }
        bool? FileLogger { get; }
        bool? FileLogger1 { get; }
        bool? FileLogger2 { get; }
        bool? FileLogger3 { get; }
        bool? FileLogger4 { get; }
        bool? FileLogger5 { get; }
        bool? FileLogger6 { get; }
        bool? FileLogger7 { get; }
        bool? FileLogger8 { get; }
        bool? FileLogger9 { get; }
        bool? DistributedFileLogger { get; }

#if DEBUG
        bool? WaitForDebugger { get; }
#endif

        // Parameterized switches
        string[]? Project { get; }
        string[]? Target { get; }
        string[]? Property { get; }
        string[]? Logger { get; }
        string[]? DistributedLogger { get; }
        string[]? Verbosity { get; }
#if FEATURE_XML_SCHEMA_VALIDATION
        string[]? Validate { get; }
#endif
        string[]? ConsoleLoggerParameters { get; }
        string[]? NodeMode { get; }
        string[]? MaxCpuCount { get; }
        string[]? IgnoreProjectExtensions { get; }
        string[]? ToolsVersion { get; }
        string[]? FileLoggerParameters { get; }
        string[]? FileLoggerParameters1 { get; }
        string[]? FileLoggerParameters2 { get; }
        string[]? FileLoggerParameters3 { get; }
        string[]? FileLoggerParameters4 { get; }
        string[]? FileLoggerParameters5 { get; }
        string[]? FileLoggerParameters6 { get; }
        string[]? FileLoggerParameters7 { get; }
        string[]? FileLoggerParameters8 { get; }
        string[]? FileLoggerParameters9 { get; }
        string[]? TerminalLogger { get; }
        string[]? TerminalLoggerParameters { get; }
        string[]? NodeReuse { get; }
        string[]? Preprocess { get; }
        string[]? Targets { get; }
        string[]? WarningsAsErrors { get; }
        string[]? WarningsNotAsErrors { get; }
        string[]? WarningsAsMessages { get; }
        string[]? BinaryLogger { get; }
        string[]? Check { get; }
        string[]? Restore { get; }
        string[]? ProfileEvaluation { get; }
        string[]? RestoreProperty { get; }
        string[]? Interactive { get; }
        string[]? IsolateProjects { get; }
        string[]? GraphBuild { get; }
        string[]? InputResultsCaches { get; }
        string[]? OutputResultsCache { get; }
#if FEATURE_REPORTFILEACCESSES
        string[]? ReportFileAccesses { get; }
#endif
        string[]? LowPriority { get; }
        string[]? Question { get; }
        string[]? DetailedSummary { get; }
        string[]? GetProperty { get; }
        string[]? GetItem { get; }
        string[]? GetTargetResult { get; }
        string[]? GetResultOutputFile { get; }
        string[]? FeatureAvailability { get; }
        string[]? MultiThreaded { get; }
    }
}
