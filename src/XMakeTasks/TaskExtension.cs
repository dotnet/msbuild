// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A small intermediate class for MSBuild tasks, see also TaskLoadInSeparateAppDomainExtension
    /// </summary>
    abstract public class TaskExtension : Task
    {
        #region Constructors

        internal TaskExtension() :
            base(AssemblyResources.PrimaryResources, "MSBuild.")
        {
            _logExtension = new TaskLoggingHelperExtension(
                this,
                AssemblyResources.PrimaryResources,
                AssemblyResources.SharedResources,
                "MSBuild.");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets an instance of a TaskLoggingHelperExtension class containing task logging methods.
        /// </summary>
        /// <value>The logging helper object.</value>
        new public TaskLoggingHelper Log
        {
            get
            {
                return _logExtension;
            }
        }

        // the logging helper
        private TaskLoggingHelperExtension _logExtension;

        #endregion
    }
}
