// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

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