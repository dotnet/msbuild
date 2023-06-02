// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This attribute is used by task writers to designate certain task parameters as "outputs". The build engine will only allow
    /// task parameters (i.e. the task class' .NET properties) that are marked with this attribute to output data from a task. Project
    /// authors can only use parameters marked with this attribute in a task's &lt;Output&gt; tag. All task parameters, including those
    /// marked with this attribute, may be treated as inputs to a task by the build engine.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class OutputAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public OutputAttribute()
        {
            // do nothing
        }
    }
}
