// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Resources;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#if !BUILD_ENGINE
using Microsoft.Build.Utilities;
#endif

//This is in the Tasks namespace because that's where it was before and it is public.

#if BUILD_ENGINE
namespace Microsoft.Build.BackEnd
#else
namespace Microsoft.Build.Tasks
#endif
{
    /// <summary>
    /// Helper logging class for tasks, used for dealing with two resource streams.
    /// </summary>
#if BUILD_ENGINE
    internal
#else
    public
#endif
    class TaskLoggingHelperExtension : TaskLoggingHelper
    {
        #region Constructors

        /// <summary>
        /// public constructor
        /// </summary>
        public TaskLoggingHelperExtension(ITask taskInstance, ResourceManager primaryResources, ResourceManager sharedResources, string helpKeywordPrefix) :
            base(taskInstance)
        {
            this.TaskResources = primaryResources;
            this.TaskSharedResources = sharedResources;
            this.HelpKeywordPrefix = helpKeywordPrefix;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Used to load culture-specific resources. Derived classes should register their resources either during construction, or
        /// via this property, if they have localized strings.
        /// </summary>
        public ResourceManager TaskSharedResources
        {
            get
            {
                return _taskSharedResources;
            }

            set
            {
                _taskSharedResources = value;
            }
        }

        // UI shared resources (including strings) used by the logging methods
        private ResourceManager _taskSharedResources;

        #endregion

        #region Utility methods

        /// <summary>
        /// Loads the specified resource string and optionally formats it using the given arguments. The current thread's culture
        /// is used for formatting.
        /// </summary>
        /// <remarks>
        /// 1) This method requires the owner task to have registered its resources either via the Task (or TaskMarshalByRef) base
        ///    class constructor, or the "Task.TaskResources" (or "AppDomainIsolatedTask.TaskResources") property.
        /// 2) This method is thread-safe.
        /// </remarks>
        /// <param name="resourceName">The name of the string resource to load.</param>
        /// <param name="args">Optional arguments for formatting the loaded string.</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>resourceName</c> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the string resource indicated by <c>resourceName</c> does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <c>TaskResources</c> property of the owner task is not set.</exception>
        override public string FormatResourceString(string resourceName, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(resourceName, nameof(resourceName));
            ErrorUtilities.VerifyThrowInvalidOperation(TaskResources != null, "Shared.TaskResourcesNotRegistered", TaskName);
            ErrorUtilities.VerifyThrowInvalidOperation(TaskSharedResources != null, "Shared.TaskResourcesNotRegistered", TaskName);

            // NOTE: the ResourceManager.GetString() method is thread-safe
            string resourceString = TaskResources.GetString(resourceName, CultureInfo.CurrentUICulture);

            if (resourceString == null)
            {
                resourceString = TaskSharedResources.GetString(resourceName, CultureInfo.CurrentUICulture);
            }

            ErrorUtilities.VerifyThrowArgument(resourceString != null, "Shared.TaskResourceNotFound", resourceName, TaskName);

            return FormatString(resourceString, args);
        }

        #endregion
    }
}
