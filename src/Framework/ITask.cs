// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines a "task" in the build system. A task is an atomic unit of build operation. All task classes must
    /// implement this interface to be recognized by the build engine.
    /// </summary>
    public interface ITask
    {
        /// <summary>
        /// This property is set by the build engine to allow a task to call back into it.
        /// </summary>
        /// <value>The interface on the build engine available to tasks.</value>
        IBuildEngine BuildEngine
        {
            get;

            set;
        }

        /// <summary>
        /// The build engine sets this property if the host IDE has associated a host object with this particular task.
        /// </summary>
        /// <value>The host object instance (can be null).</value>
        ITaskHost HostObject
        {
            get;

            set;
        }

        /// <summary>
        /// This method is called by the build engine to begin task execution. A task uses the return value to indicate
        /// whether it was successful. If a task throws an exception out of this method, the engine will automatically
        /// assume that the task has failed.
        /// </summary>
        /// <returns>true, if successful</returns>
        bool Execute();
    }
}
