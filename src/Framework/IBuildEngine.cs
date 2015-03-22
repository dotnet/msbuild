// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface exposes functionality on the build engine
    /// that is required for task authoring.
    /// </summary>
    public interface IBuildEngine
    {
        /// <summary>
        /// Allows tasks to raise error events to all registered loggers.
        /// The build engine may perform some filtering or
        /// pre-processing on the events, before dispatching them.
        /// </summary>
        /// <param name="e">Details of event to raise.</param>
        void LogErrorEvent(BuildErrorEventArgs e);

        /// <summary>
        /// Allows tasks to raise warning events to all registered loggers.
        /// The build engine may perform some filtering or
        /// pre-processing on the events, before dispatching them.
        /// </summary>
        /// <param name="e">Details of event to raise.</param>
        void LogWarningEvent(BuildWarningEventArgs e);

        /// <summary>
        /// Allows tasks to raise message events to all registered loggers.
        /// The build engine may perform some filtering or
        /// pre-processing on the events, before dispatching them.
        /// </summary>
        /// <param name="e">Details of event to raise.</param>
        void LogMessageEvent(BuildMessageEventArgs e);

        /// <summary>
        /// Allows tasks to raise custom events to all registered loggers.
        /// The build engine may perform some filtering or
        /// pre-processing on the events, before dispatching them.
        /// </summary>
        /// <param name="e">Details of event to raise.</param>
        void LogCustomEvent(CustomBuildEventArgs e);

        /// <summary>
        /// Returns true if the ContinueOnError flag was set to true for this particular task
        /// in the project file.
        /// </summary>
        bool ContinueOnError
        {
            get;
        }

        /// <summary>
        /// Retrieves the line number of the task node within the project file that called it.
        /// </summary>
        int LineNumberOfTaskNode
        {
            get;
        }

        /// <summary>
        /// Retrieves the line number of the task node within the project file that called it.
        /// </summary>
        int ColumnNumberOfTaskNode
        {
            get;
        }

        /// <summary>
        /// Returns the full path to the project file that contained the call to this task.
        /// </summary>
        string ProjectFileOfTaskNode
        {
            get;
        }

        /// <summary>
        /// This method allows tasks to initiate a build on a
        /// particular project file. If the build is successful, the outputs
        /// (if any) of the specified targets are returned.
        /// </summary>
        /// <remarks>
        /// 1) it is acceptable to pass null for both <c>targetNames</c> and <c>targetOutputs</c>
        /// 2) if no targets are specified, the default targets are built
        /// 3) target outputs are returned as <c>ITaskItem</c> arrays indexed by target name
        /// </remarks>
        /// <param name="projectFileName">The project to build.</param>
        /// <param name="targetNames">The targets in the project to build (can be null).</param>
        /// <param name="globalProperties">A hash table of additional global properties to apply
        ///     to the child project (can be null).  The key and value should both be strings.</param>
        /// <param name="targetOutputs">The outputs of each specified target (can be null).</param>
        /// <returns>true, if build was successful</returns>
        bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs
            );
    }
}
