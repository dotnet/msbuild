// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Resources;
using System.Security;

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class provides the same functionality as the Task class, but derives from MarshalByRefObject so that it can be
    /// instantiated in its own app domain.
    /// </summary>
    [LoadInSeparateAppDomain]
#if !FEATURE_APPDOMAIN
    [Obsolete("AppDomains are no longer supported in .NET Core or .NET 5.0 or higher.")]
#endif
    public abstract class AppDomainIsolatedTask : MarshalByRefObject, ITask
    {
        #region Constructors

        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected AppDomainIsolatedTask()
        {
            Log = new TaskLoggingHelper(this);
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        protected AppDomainIsolatedTask(ResourceManager taskResources)
            : this()
        {
            Log.TaskResources = taskResources;
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources, as well as provide a prefix for
        /// composing help keywords from string resource names. If the prefix is an empty string, then string resource names will
        /// be used verbatim as help keywords. For an example of how the prefix is used, see the
        /// <see cref="TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[])"/> method.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix.</param>
        protected AppDomainIsolatedTask(ResourceManager taskResources, string helpKeywordPrefix)
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

        /// <summary>
        /// The build engine sets this property if the host IDE has associated a host object with this particular task.
        /// </summary>
        /// <value>The host object instance (can be null).</value>
        public ITaskHost HostObject { get; set; }

        /// <summary>
        /// Gets an instance of a TaskLoggingHelper class containing task logging methods.
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
        /// see the <see cref="TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[])"/> method.
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

        /// <summary>
        /// Overridden to give tasks deriving from this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and task instances can expire if they take long time processing.
        /// </summary>
        [SecurityCritical]
#pragma warning disable CS0809 // InitializeLifetimeService is not marked as obsolete in netstandard2.0
#if !FEATURE_APPDOMAIN
        // This Obsolete is redundant since the whole class is obsoleted, but required to guard the reference
        // to the obsolete MarshalByRefObject.InitializeLifetimeService.
        [Obsolete("AppDomains are no longer supported in .NET Core or .NET 5.0 or higher.")]
#endif
        public override object InitializeLifetimeService() => null; // null means infinite lease time
#pragma warning restore

        #endregion
    }
}
