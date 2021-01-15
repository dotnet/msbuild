// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Enumeration of the levels of detail of an event log.
    /// </summary>
    /// <remarks>
    /// The level of detail (i.e. verbosity) of an event log is entirely controlled by the logger generating the log -- a logger
    /// will be directed to keep its verbosity at a certain level, based on user preferences, but a logger is free to choose the
    /// events it logs for each verbosity level.
    /// 
    /// LOGGING GUIDELINES FOR EACH VERBOSITY LEVEL:
    /// 1) Quiet -- only display a summary at the end of build
    /// 2) Minimal -- only display errors, warnings, high importance events and a build summary
    /// 3) Normal -- display all errors, warnings, high importance events, some status events, and a build summary
    /// 4) Detailed -- display all errors, warnings, high and normal importance events, all status events, and a build summary
    /// 5) Diagnostic -- display all events, and a build summary
    /// </remarks>
    // 
    // WARNING: VS Automation code for the Tools/Options MSBuild build verbosity setting will be broken
    // by changes to this enum (not to mention existing MSBuild clients and vsproject code). 
    // Please make sure to talk to automation devs before changing it.

    [ComVisible(true)]
    public enum LoggerVerbosity
    {
        /// <summary>
        /// The most minimal output
        /// </summary>
        Quiet,

        /// <summary>
        /// Relatively little output
        /// </summary>
        Minimal,

        /// <summary>
        /// Standard output. This should be the default if verbosity level is not set
        /// </summary>
        Normal,

        /// <summary>
        /// Relatively verbose, but not exhaustive
        /// </summary>
        Detailed,

        /// <summary>
        /// The most verbose and informative verbosity
        /// </summary>
        Diagnostic
    }

    /// <summary>
    /// This interface defines a "logger" in the build system. A logger subscribes to build system events. All logger classes must
    /// implement this interface to be recognized by the build engine.
    /// </summary>
    [ComVisible(true)]
    public interface ILogger
    {
        /// <summary>
        /// The verbosity level directs the amount of detail that appears in a logger's event log. Though this is only a
        /// recommendation based on user preferences, and a logger is free to choose the exact events it logs, it is still
        /// important that the guidelines for each level be followed, for a good user experience.
        /// </summary>
        /// <value>The verbosity level.</value>
        LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// This property holds the user-specified parameters to the logger. If parameters are not provided, a logger should revert
        /// to defaults. If a logger does not take parameters, it can ignore this property.
        /// </summary>
        /// <value>The parameter string (can be null).</value>
        string Parameters { get; set; }

        /// <summary>
        /// Called by the build engine to allow loggers to subscribe to the events they desire.
        /// </summary>
        /// <param name="eventSource">The events available to loggers.</param>
        void Initialize(IEventSource eventSource);

        /// <summary>
        /// Called by the build engine to allow loggers to release any resources they may have allocated at initialization time,
        /// or during the build.
        /// </summary>
        void Shutdown();
    }
}
