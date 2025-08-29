﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class provides the same functionality as the Task class, but derives from MarshalByRefObject so that it can be
    /// instantiated in its own app domain.
    /// </summary>
    [LoadInSeparateAppDomain]
    public abstract class AppDomainIsolatedTaskExtension : AppDomainIsolatedTask
    {
        #region Constructors

        internal AppDomainIsolatedTaskExtension() :
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
#endif
