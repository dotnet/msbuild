// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends IBuildEngine to provide a method allowing building
    /// project files in parallel.
    /// </summary>
    public interface IBuildEngine3 : IBuildEngine2
    {
        /// <summary>
        /// This method allows tasks to initiate a build on a
        /// particular project file. If the build is successful, the outputs
        /// (if any) of the specified targets are returned.
        /// </summary>
        /// <remarks>
        /// 1) it is acceptable to pass null for both <c>targetNames</c> and <c>targetOutputs</c>
        /// 2) if no targets are specified, the default targets are built
        ///
        /// </remarks>
        /// <param name="projectFileNames">The project to build.</param>
        /// <param name="targetNames">The targets in the project to build (can be null).</param>
        /// <param name="globalProperties">An array of hashtables of additional global properties to apply
        ///     to the child project (array entries can be null).
        ///     The key and value in the hashtable should both be strings.</param>
        /// <param name="removeGlobalProperties">A list of global properties which should be removed.</param>
        /// <param name="toolsVersion">A tools version recognized by the Engine that will be used during this build (can be null).</param>
        /// <param name="returnTargetOutputs">Should the target outputs be returned in the BuildEngineResult</param>
        /// <returns>Returns a structure containing the success or failure of the build and the target outputs by project.</returns>
        BuildEngineResult BuildProjectFilesInParallel(
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersion,
            bool returnTargetOutputs);

        /// <summary>
        /// Informs the system that this task has a long-running out-of-process component and other work can be done in the
        /// build while that work completes.
        /// </summary>
        /// <remarks>
        /// After calling <see cref="Yield"/>, global process state like environment variables and current working directory 
        /// can change arbitrarily until <see cref="Reacquire"/> returns. As a result, if you are going to depend on any of 
        /// that state, for instance by opening files by relative path, rather than calling 
        /// <c>ITaskItem.GetMetadata("FullPath")</c>, you must do so before calling <see cref="Yield"/>. 
        /// The recommended pattern is to figure out what all the long-running work is and start it before yielding.
        /// </remarks>
        void Yield();

        /// <summary>
        /// Waits to reacquire control after yielding.
        /// </summary>
        /// <remarks>
        /// This method must be called to regain control after <see cref="Yield"/> has been called.
        /// </remarks>
        void Reacquire();
    }
}
