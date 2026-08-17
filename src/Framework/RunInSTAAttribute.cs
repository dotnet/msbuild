// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This attribute is used to mark a task class as being required to run in a Single Threaded Apartment for COM.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "STA", Justification = "It is cased correctly.")]
    public sealed class RunInSTAAttribute : Attribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public RunInSTAAttribute()
        {
            // do nothing
        }
    }
}
