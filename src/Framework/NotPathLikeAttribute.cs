// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class defines the attribute that a task writer can apply to a task's property to declare the property to be not path-like.
    /// When a string (or string[]) property marked with this attribute is constructed, it will not be subject to path normalization during expansion.
    /// When an ITaskItem (or ITaskItem[]) property marked with this attribute is constructed, its metadata will not be subject to path normalization during expansion.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NotPathLikeAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NotPathLikeAttribute()
        {
        }
    }
}
