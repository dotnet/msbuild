// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A small intermediate class for MSBuild tasks, see also TaskLoadInSeparateAppDomainExtension
    /// </summary>
    public abstract class TaskExtension : Task, ITaskParameterLoggingOptions
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

        /// <summary>
        /// Task implementations can override this to customize the logging of input and output parameters.
        /// </summary>
        /// <param name="parameterName">Name of a parameter to get the options for.</param>
        /// <returns>A struct indicating whether to log the parameter, and if yes and it's an item list, whether to log metadata for each item.</returns>
        internal virtual ParameterLoggingOptions GetParameterLoggingOptions(string parameterName) => default;

        ParameterLoggingOptions ITaskParameterLoggingOptions.GetParameterLoggingOptions(string parameterName)
            => GetParameterLoggingOptions(parameterName);
    }
}
