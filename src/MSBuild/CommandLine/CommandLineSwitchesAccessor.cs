// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static Microsoft.Build.CommandLine.Experimental.CommandLineSwitches;

namespace Microsoft.Build.CommandLine.Experimental
{
    internal readonly struct CommandLineSwitchesAccessor
    {
        private readonly CommandLineSwitches switches;

        internal CommandLineSwitchesAccessor(CommandLineSwitches switches)
        {
            this.switches = switches;
        }

        // Parameterless switches
        public bool? Help => GetParameterlessSwitchValue(ParameterlessSwitch.Help);

        public bool? Version => GetParameterlessSwitchValue(ParameterlessSwitch.Version);

        public bool? NoLogo => GetParameterlessSwitchValue(ParameterlessSwitch.NoLogo);

        public bool? NoAutoResponse => GetParameterlessSwitchValue(ParameterlessSwitch.NoAutoResponse);

        public bool? NoConsoleLogger => GetParameterlessSwitchValue(ParameterlessSwitch.NoConsoleLogger);

        public bool? FileLogger => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger);

        public bool? FileLogger1 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger1);

        public bool? FileLogger2 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger2);

        public bool? FileLogger3 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger3);

        public bool? FileLogger4 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger4);

        public bool? FileLogger5 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger5);

        public bool? FileLogger6 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger6);

        public bool? FileLogger7 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger7);

        public bool? FileLogger8 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger8);

        public bool? FileLogger9 => GetParameterlessSwitchValue(ParameterlessSwitch.FileLogger9);

        public bool? DistributedFileLogger => GetParameterlessSwitchValue(ParameterlessSwitch.DistributedFileLogger);

#if DEBUG
        public bool? WaitForDebugger => GetParameterlessSwitchValue(ParameterlessSwitch.WaitForDebugger);
#endif

        // Parameterized switches
        public string[]? Project => GetParameterizedSwitchValue(ParameterizedSwitch.Project);

        public string[]? Target => GetParameterizedSwitchValue(ParameterizedSwitch.Target);

        public string[]? Property => GetParameterizedSwitchValue(ParameterizedSwitch.Property);

        public string[]? Logger => GetParameterizedSwitchValue(ParameterizedSwitch.Logger);

        public string[]? DistributedLogger => GetParameterizedSwitchValue(ParameterizedSwitch.DistributedLogger);

        public string[]? Verbosity => GetParameterizedSwitchValue(ParameterizedSwitch.Verbosity);

#if FEATURE_XML_SCHEMA_VALIDATION
        public string[]? Validate => GetParameterizedSwitchValue(ParameterizedSwitch.Validate);
#endif

        public string[]? ConsoleLoggerParameters => GetParameterizedSwitchValue(ParameterizedSwitch.ConsoleLoggerParameters);

        public string[]? NodeMode => GetParameterizedSwitchValue(ParameterizedSwitch.NodeMode);

        public string[]? MaxCpuCount => GetParameterizedSwitchValue(ParameterizedSwitch.MaxCPUCount);

        public string[]? IgnoreProjectExtensions => GetParameterizedSwitchValue(ParameterizedSwitch.IgnoreProjectExtensions);

        public string[]? ToolsVersion => GetParameterizedSwitchValue(ParameterizedSwitch.ToolsVersion);

        public string[]? FileLoggerParameters => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters);

        public string[]? FileLoggerParameters1 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters1);

        public string[]? FileLoggerParameters2 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters2);

        public string[]? FileLoggerParameters3 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters3);

        public string[]? FileLoggerParameters4 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters4);

        public string[]? FileLoggerParameters5 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters5);

        public string[]? FileLoggerParameters6 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters6);

        public string[]? FileLoggerParameters7 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters7);

        public string[]? FileLoggerParameters8 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters8);

        public string[]? FileLoggerParameters9 => GetParameterizedSwitchValue(ParameterizedSwitch.FileLoggerParameters9);

        public string[]? TerminalLogger => GetParameterizedSwitchValue(ParameterizedSwitch.TerminalLogger);

        public string[]? TerminalLoggerParameters => GetParameterizedSwitchValue(ParameterizedSwitch.TerminalLoggerParameters);

        public string[]? NodeReuse => GetParameterizedSwitchValue(ParameterizedSwitch.NodeReuse);

        public string[]? Preprocess => GetParameterizedSwitchValue(ParameterizedSwitch.Preprocess);

        public string[]? Targets => GetParameterizedSwitchValue(ParameterizedSwitch.Targets);

        public string[]? WarningsAsErrors => GetParameterizedSwitchValue(ParameterizedSwitch.WarningsAsErrors);

        public string[]? WarningsNotAsErrors => GetParameterizedSwitchValue(ParameterizedSwitch.WarningsNotAsErrors);

        public string[]? WarningsAsMessages => GetParameterizedSwitchValue(ParameterizedSwitch.WarningsAsMessages);

        public string[]? BinaryLogger => GetParameterizedSwitchValue(ParameterizedSwitch.BinaryLogger);

        public string[]? Check => GetParameterizedSwitchValue(ParameterizedSwitch.Check);

        public string[]? Restore => GetParameterizedSwitchValue(ParameterizedSwitch.Restore);

        public string[]? ProfileEvaluation => GetParameterizedSwitchValue(ParameterizedSwitch.ProfileEvaluation);

        public string[]? RestoreProperty => GetParameterizedSwitchValue(ParameterizedSwitch.RestoreProperty);

        public string[]? Interactive => GetParameterizedSwitchValue(ParameterizedSwitch.Interactive);

        public string[]? IsolateProjects => GetParameterizedSwitchValue(ParameterizedSwitch.IsolateProjects);

        public string[]? GraphBuild => GetParameterizedSwitchValue(ParameterizedSwitch.GraphBuild);

        public string[]? InputResultsCaches => GetParameterizedSwitchValue(ParameterizedSwitch.InputResultsCaches);

        public string[]? OutputResultsCache => GetParameterizedSwitchValue(ParameterizedSwitch.OutputResultsCache);

#if FEATURE_REPORTFILEACCESSES
        public string[]? ReportFileAccesses => GetParameterizedSwitchValue(ParameterizedSwitch.ReportFileAccesses);
#endif

        public string[]? LowPriority => GetParameterizedSwitchValue(ParameterizedSwitch.LowPriority);

        public string[]? Question => GetParameterizedSwitchValue(ParameterizedSwitch.Question);

        public string[]? DetailedSummary => GetParameterizedSwitchValue(ParameterizedSwitch.DetailedSummary);

        public string[]? GetProperty => GetParameterizedSwitchValue(ParameterizedSwitch.GetProperty);

        public string[]? GetItem => GetParameterizedSwitchValue(ParameterizedSwitch.GetItem);

        public string[]? GetTargetResult => GetParameterizedSwitchValue(ParameterizedSwitch.GetTargetResult);

        public string[]? GetResultOutputFile => GetParameterizedSwitchValue(ParameterizedSwitch.GetResultOutputFile);

        public string[]? FeatureAvailability => GetParameterizedSwitchValue(ParameterizedSwitch.FeatureAvailability);

        public string[]? MultiThreaded => GetParameterizedSwitchValue(ParameterizedSwitch.MultiThreaded);

        private bool? GetParameterlessSwitchValue(ParameterlessSwitch switchType) => switches.IsParameterlessSwitchSet(switchType) ? switches[switchType] : null;

        private string[]? GetParameterizedSwitchValue(ParameterizedSwitch switchType) => switches.IsParameterizedSwitchSet(switchType) ? switches[switchType] : null;
    }
}
