// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This attribute is used to mark tasks that need to be run in their own app domains. The build engine will create a new app
    /// domain each time it needs to run such a task, and immediately unload it when the task is finished.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class LoadInSeparateAppDomainAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LoadInSeparateAppDomainAttribute()
        {
            // do nothing
        }
    }
}
