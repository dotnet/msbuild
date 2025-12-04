// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A small intermediate class for MSBuild tasks, see also TaskLoadInSeparateAppDomainExtension
    /// </summary>
    public abstract class TaskExtension : Task
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
        public new TaskLoggingHelper Log => _logExtension;

        // the logging helper
        private readonly TaskLoggingHelperExtension _logExtension;

        #endregion
    }
}
