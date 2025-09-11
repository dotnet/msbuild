// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Attribute that marks a task class as thread-safe.
    /// Task classes marked with this attribute indicate they can be safely executed in parallel in the same process with other tasks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class MSBuildMultiThreadableTaskAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ThreadSafeAttribute class.
        /// </summary>
        public MSBuildMultiThreadableTaskAttribute()
        {
        }
    }
}
