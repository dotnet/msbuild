// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends IBuildEngine to provide a method allowing building 
    /// project files in parallel.
    /// </summary>
    public interface IBuildEngine2 : IBuildEngine
    {
        /// <summary>
        /// This property allows a task to query whether or not the system is running in single process mode or multi process mode.
        /// Single process mode (IsRunningMultipleNodes = false) is where the engine is initialized with the number of cpus = 1 and the engine is not a child engine.
        /// The engine is in multi process mode (IsRunningMultipleNodes = true) when the engine is initialized with a number of cpus > 1 or the engine is a child engine.
        /// </summary>
        bool IsRunningMultipleNodes
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
        /// <param name="toolsVersion">A tools version recognized by the Engine that will be used during this build (can be null).</param>
        /// <returns>true, if build was successful</returns>
        bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs,
            string toolsVersion
            );

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
        /// <param name="projectFileNames">The project to build.</param>
        /// <param name="targetNames">The targets in the project to build (can be null).</param>
        /// <param name="globalProperties">An array of hashtables of additional global properties to apply
        ///     to the child project (array entries can be null). 
        ///     The key and value in the hashtable should both be strings.</param>
        /// <param name="targetOutputsPerProject">The outputs of each specified target (can be null).</param>
        /// <param name="toolsVersion">A tools version recognized by the Engine that will be used during this build (can be null).</param>
        /// <param name="useResultsCache">If true the operation will only be run if the cache doesn't
        ///                               already contain the result. After the operation the result is
        ///                               stored in the cache </param>
        /// <param name="unloadProjectsOnCompletion">If true the project will be unloaded once the 
        ///                                         operation is completed </param>
        /// <returns>true, if build was successful</returns>
        bool BuildProjectFilesInParallel
            (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
            );
    }
}
