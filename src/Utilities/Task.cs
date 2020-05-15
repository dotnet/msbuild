// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;

using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This helper base class provides default functionality for tasks. This class can only be instantiated in a derived form.
    /// </summary>
    public abstract class Task : ITask
    {
        #region Constructors

        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected Task()
        {
            Log = new TaskLoggingHelper(this);
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        protected Task(ResourceManager taskResources)
            : this()
        {
            Log.TaskResources = taskResources;
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources, as well as provide a prefix for
        /// composing help keywords from string resource names. If the prefix is an empty string, then string resource names will
        /// be used verbatim as help keywords. For an example of how the prefix is used, see the
        /// TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[]) method.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix.</param>
        protected Task(ResourceManager taskResources, string helpKeywordPrefix)
            : this(taskResources)
        {
            Log.HelpKeywordPrefix = helpKeywordPrefix;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The build engine automatically sets this property to allow tasks to call back into it.
        /// </summary>
        /// <value>The build engine interface available to tasks.</value>
        public IBuildEngine BuildEngine { get; set; }

        // The casts below are always possible because this class is built against the 
        // Orcas Framework assembly or later, so the version of MSBuild that does not
        // know about IBuildEngine2 will never load it.
        // No setters needed; the Engine always sets through the BuildEngine setter

        /// <summary>
        /// The build engine automatically sets this property to allow tasks to call back into it.
        /// This is a convenience property so that task authors inheriting from this class do not
        /// have to cast the value from IBuildEngine to IBuildEngine2.
        /// </summary>
        /// <value>The build engine interface available to tasks.</value>
        public IBuildEngine2 BuildEngine2 => (IBuildEngine2)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine3" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine3 BuildEngine3 => (IBuildEngine3)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine4" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine4 BuildEngine4 => (IBuildEngine4)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine5" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine5 BuildEngine5 => (IBuildEngine5)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine6" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine6 BuildEngine6 => (IBuildEngine6)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine7" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine7 BuildEngine7 => (IBuildEngine7)BuildEngine;

        /// <summary>
        /// The build engine sets this property if the host IDE has associated a host object with this particular task.
        /// </summary>
        /// <value>The host object instance (can be null).</value>
        public ITaskHost HostObject { get; set; }

        /// <summary>
        /// Gets an instance of a TaskLoggingHelper class containing task logging methods.
        /// The taskLoggingHelper is a MarshallByRef object which needs to have MarkAsInactive called
        /// if the parent task is making the appdomain and marshaling this object into it. If the appdomain is not unloaded at the end of 
        /// the task execution and the MarkAsInactive method is not called this will result in a leak of the task instances in the appdomain the task was created within.
        /// </summary>
        /// <value>The logging helper object.</value>
        public TaskLoggingHelper Log { get; }

        /// <summary>
        /// Gets or sets the task's culture-specific resources. Derived classes should register their resources either during
        /// construction, or via this property, if they have localized strings.
        /// </summary>
        /// <value>The task's resources (can be null).</value>
        protected ResourceManager TaskResources
        {
            get => Log.TaskResources;
            set => Log.TaskResources = value;
        }

        /// <summary>
        /// Gets or sets the prefix used to compose help keywords from string resource names. If a task does not have help
        /// keywords associated with its messages, it can ignore this property or set it to null. If the prefix is set to an empty
        /// string, then string resource names will be used verbatim as help keywords. For an example of how this prefix is used,
        /// see the TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[]) method.
        /// </summary>
        /// <value>The help keyword prefix string (can be null).</value>
        protected string HelpKeywordPrefix
        {
            get => Log.HelpKeywordPrefix;
            set => Log.HelpKeywordPrefix = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Must be implemented by derived class.
        /// </summary>
        /// <returns>true, if successful</returns>
        public abstract bool Execute();

        #endregion
    }
}
