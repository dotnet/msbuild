// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// When marked with the RequiredRuntimeAttribute, a task indicates that it has stricter 
    /// runtime requirements than a regular task - this tells MSBuild that it will need to potentially 
    /// launch a separate process for that task if the current runtime does not match the version requirement.
    /// This attribute is currently non-functional since there is only one version of the CLR that is
    /// capable of running MSBuild v2.0 or v3.5 - the runtime v2.0 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredRuntimeAttribute : Attribute
    {
        /// <summary>
        /// Constructor taking a version, such as "v2.0". 
        /// </summary>
        public RequiredRuntimeAttribute(string runtimeVersion)
        {
            _runtimeVersion = runtimeVersion;
        }

        private string _runtimeVersion;

        /// <summary>
        /// Returns the runtime version the attribute was constructed with,
        /// e.g., "v2.0"
        /// </summary>
        public string RuntimeVersion
        {
            get
            {
                return _runtimeVersion;
            }
        }
    }
}
