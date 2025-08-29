// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class defines the attribute that a task writer can apply to a task's property to declare the property to be a
    /// required property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public RequiredAttribute()
        {
        }
    }
}
