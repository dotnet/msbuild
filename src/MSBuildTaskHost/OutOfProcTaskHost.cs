// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Entry point class for MSBuildTaskHost.exe, which serves as the 
// task host executable for CLR 2 tasks.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

// CR: We could move MSBuildApp.ExitType out of MSBuildApp
using Microsoft.Build.CommandLine;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This is the Out-Of-Proc Task Host for supporting Cross-Targeting tasks.
    /// </summary>
    /// <remarks>
    /// It will be responsible for:
    /// - Task execution
    /// - Communicating with the MSBuildApp process, specifically the TaskHostFactory
    ///   (Logging messages, receiving Tasks from TaskHostFactory, sending results and other messages)
    /// </remarks>
    public static class OutOfProcTaskHost
    {
        /// <summary>
        /// Enumeration of the various ways in which the MSBuildTaskHost.exe application can exit.
        /// </summary>
        internal enum ExitType
        {
            /// <summary>
            /// The application executed successfully.
            /// </summary>
            Success,

            /// <summary>
            /// We received a request from MSBuild.exe to terminate
            /// </summary>
            TerminateRequest,

            /// <summary>
            /// A logger aborted the build.
            /// </summary>
            LoggerAbort,

            /// <summary>
            /// A logger failed unexpectedly.
            /// </summary>
            LoggerFailure,

            /// <summary>
            /// The Task Host Node did not terminate gracefully
            /// </summary>
            TaskHostNodeFailed,

            /// <summary>
            /// An unexpected failure
            /// </summary>
            Unexpected
        }

        /// <summary>
        /// Main Entry Point
        /// </summary>
        /// <remarks>
        /// We won't execute any tasks in the main thread, so we don't need to be in an STA
        /// </remarks>
        [MTAThread]
        public static int Main()
        {
            int exitCode = (Execute() == ExitType.Success ? 0 : 1);
            return exitCode;
        }

        /// <summary>
        /// Orchestrates the execution of the application.
        /// Also responsible for top-level error handling.
        /// </summary>
        /// <returns>
        /// A value of Success if the bootstrapping succeeds
        /// </returns>
        internal static ExitType Execute()
        {
#if FEATURE_DEBUG_LAUNCH
            // Provide Hook for debugger
            if (Environment.GetEnvironmentVariable("MSBUILDDEBUGONSTART") == "1")
            {
                Debugger.Launch();
            }
#endif

            bool restart = false;
            do
            {
                OutOfProcTaskHostNode oopTaskHostNode = new OutOfProcTaskHostNode();
                Exception taskHostShutDownException = null;
                NodeEngineShutdownReason taskHostShutDownReason = oopTaskHostNode.Run(out taskHostShutDownException);

                if (taskHostShutDownException != null)
                {
                    return ExitType.TaskHostNodeFailed;
                }

                switch (taskHostShutDownReason)
                {
                    case NodeEngineShutdownReason.BuildComplete:
                        return ExitType.Success;

                    case NodeEngineShutdownReason.BuildCompleteReuse:
                        restart = true;
                        break;

                    default:
                        return ExitType.TaskHostNodeFailed;
                }
            }
            while (restart);

            // Should not happen
            ErrorUtilities.ThrowInternalErrorUnreachable();
            return ExitType.Unexpected;
        }
    }
}
