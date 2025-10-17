// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Internal marker attribute for tasks that are part of the MSBuild repository.
    /// When a task inherits from a base class marked with this attribute, IMultiThreadableTask
    /// is treated with non-inheritable semantics to maintain backward compatibility.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    internal sealed class InternalMSBuildTaskAttribute : Attribute
    {
    }
}
