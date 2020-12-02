// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Manages full tracking activation and suspension.
    /// </summary>
    internal class FullTracking : IDisposable
    {
        /// <summary>
        /// The default name of the MSBuild property to read for the relative path to the full tracking .tlog files.
        /// If this property isn't set in the project, full tracking is turned off.
        /// </summary>
        private const string FullTrackingDirectoryPropertyName = "MSBuildFullTrackingPath";

        /// <summary>
        /// The full path to where full tracking .tlog files should be written.
        /// </summary>
        private string _tlogDirectory;

        /// <summary>
        /// A value indicating whether this instance is tracking a full tracking suspension
        /// (as opposed to activation).
        /// </summary>
        private TrackingMode _trackingMode;

        /// <summary>
        /// The name of the task as given to FileTracker.dll.
        /// </summary>
        private string _taskName;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTracking"/> class.
        /// </summary>
        private FullTracking()
        {
            _trackingMode = TrackingMode.None;
        }

        /// <summary>
        /// The state of the <see cref="FullTracking"/> object regarding whether it is actively tracking or suspending tracking.
        /// </summary>
        private enum TrackingMode
        {
            /// <summary>
            /// No tracking or suspension operation is in effect as a result of this instance.
            /// </summary>
            None,

            /// <summary>
            /// This instance has invoked full tracking.
            /// </summary>
            Active,

            /// <summary>
            /// This instance has suspended full tracking.
            /// </summary>
            Suspended,
        }

        /// <summary>
        /// Disposes the FullTracking object, causing full tracking to end, or resume,
        /// depending on how this object was created.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts full tracking.
        /// </summary>
        /// <param name="targetName">taskLoggingContext.TargetLoggingContext.Target.Name</param>
        /// <param name="taskName">taskNode.Name</param>
        /// <param name="projectRootDirectory">buildRequestEntry.ProjectRootDirectory</param>
        /// <param name="projectProperties">buildRequestEntry.RequestConfiguration.Project.PropertiesToBuildWith</param>
        /// <returns>
        /// An object that will stop full tracking when disposed.
        /// </returns>
        internal static IDisposable Track(string targetName, string taskName, string projectRootDirectory, PropertyDictionary<ProjectPropertyInstance> projectProperties)
        {
            FullTracking tracking = new FullTracking();

            ProjectPropertyInstance tlogRelativeDirectoryProperty = projectProperties[FullTrackingDirectoryPropertyName];
            string tlogRelativeDirectoryValue = null;
            if (tlogRelativeDirectoryProperty != null)
            {
                tlogRelativeDirectoryValue = tlogRelativeDirectoryProperty.EvaluatedValue;
            }

            if (!String.IsNullOrEmpty(tlogRelativeDirectoryValue))
            {
                tracking._taskName = GenerateUniqueTaskName(targetName, taskName);
                tracking._tlogDirectory = Path.Combine(projectRootDirectory, tlogRelativeDirectoryValue);
                InprocTrackingNativeMethods.StartTrackingContext(tracking._tlogDirectory, tracking._taskName);
                tracking._trackingMode = TrackingMode.Active;
            }

            return tracking;
        }

        /// <summary>
        /// Suspends full tracking.
        /// </summary>
        /// <returns>An object that will resume full tracking when disposed.</returns>
        internal static IDisposable Suspend()
        {
            FullTracking tracking = new FullTracking();

            if (tracking._trackingMode == TrackingMode.Active)
            {
                InprocTrackingNativeMethods.SuspendTracking();
                tracking._trackingMode = TrackingMode.Suspended;
            }

            return tracking;
        }

        /// <summary>
        /// Disposes the FullTracking object, causing full tracking to end, or resume,
        /// depending on how this object was created.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                switch (_trackingMode)
                {
                    case TrackingMode.Active:
                        // Stop tracking
                        InprocTrackingNativeMethods.WriteContextTLogs(_tlogDirectory, _taskName);
                        InprocTrackingNativeMethods.EndTrackingContext();
                        break;
                    case TrackingMode.Suspended:
                        // Stop suspending tracking
                        InprocTrackingNativeMethods.ResumeTracking();
                        break;
                    default:
                        // nothing to do here if we weren't actively tracking or suspended.
                        break;
                }
            }
        }

        /// <summary>
        /// Gets the task name to pass to Tracker.
        /// </summary>
        private static string GenerateUniqueTaskName(string targetName, string taskName)
        {
            return "__FT__" + targetName + "-" + taskName;
        }
    }
}

#endif
