// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Diagnostic information that a build logger cares about.
    /// </summary>
    [Flags]
    public enum DiagnosticInformation
    {
        /// <summary>
        /// No events.
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Build started
        /// </summary>
        BuildStartedEvent = 0x1,

        /// <summary>
        /// Build finished
        /// </summary>
        BuildFinishedEvent = 0x2,

        /// <summary>
        /// Project started
        /// </summary>
        ProjectStartedEvent = 0x4,

        /// <summary>
        /// Project finished
        /// </summary>
        ProjectFinishedEvent = 0x8,

        /// <summary>
        /// Project evaluation started
        /// </summary>
        ProjectEvaluationStartedEvent = 0x10,

        /// <summary>
        /// Project evaluation finished
        /// </summary>
        ProjectEvaluationFinishedEvent = 0x20,

        /// <summary>
        /// Target started
        /// </summary>
        TargetStartedEvent = 0x40,

        /// <summary>
        /// Target finished
        /// </summary>
        TargetFinishedEvent = 0x80,

        /// <summary>
        /// Task started
        /// </summary>
        TaskStartedEvent = 0x100,

        /// <summary>
        /// Task finished
        /// </summary>
        TaskFinishedEvent = 0x200,

        /// <summary>
        /// Error
        /// </summary>
        ErrorEvent = 0x400,

        /// <summary>
        /// Warning
        /// </summary>
        WarningEvent = 0x800,

        /// <summary>
        /// High message
        /// </summary>
        HighMessageEvent = 0x1000,

        /// <summary>
        /// Normal message
        /// </summary>
        NormalMessageEvent = 0x2000,

        /// <summary>
        /// Low message
        /// </summary>
        LowMessageEvent = 0x4000,

        /// <summary>
        /// Custom
        /// </summary>
        CustomEvent = 0x8000,

        /// <summary>
        /// Command line
        /// </summary>
        CommandLine = 0x10000,

        /// <summary>
        /// Performance summary
        /// </summary>
        PerformanceSummary = 0x20000,

        /// <summary>
        /// No summary
        /// </summary>
        NoSummary = 0x40000,

        /// <summary>
        /// Show command line
        /// </summary>
        ShowCommandLine = 0x80000,

        /// <summary>
        /// Log task inputs
        /// </summary>
        TaskInputs = 0x100000,

        /// <summary>
        /// Log evaluation profiles
        /// </summary>
        EvaluationProfile = 0x200000
    }

    /// <summary>
    /// A logger that specifies at a specific level what information it wants to be logged. This is
    /// useful to specify to a forwarding logger exactly what events are to be forwarded, and also
    /// allows turning on task input logging and evaluation profiling.
    /// </summary>
    public interface IDiagnosticLogger : ILogger
    {
        DiagnosticInformation DiagnosticInformation { get; }
    }
}
