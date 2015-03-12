// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
