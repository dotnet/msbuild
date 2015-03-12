// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This empty interface is used to pass host objects from an IDE to individual
    /// tasks.  Depending on the task itself and what kinds parameters and functionality
    /// it exposes, the task should define its own interface that inherits from this one, 
    /// and then use that interface to communicate with the host.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("9049A481-D0E9-414f-8F92-D4F67A0359A6")]
    [SuppressMessage("Microsoft.Design", "CA1040:AvoidEmptyInterfaces", Justification = "This empty interface is used to pass host objects from an IDE to individual")]
    public interface ITaskHost
    {
    }
}
