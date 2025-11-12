// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides a COM-compatible interface for passing task items from Visual Studio 
    /// to MSBuild tasks running in out-of-process scenarios via the Running Object Table (ROT).
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("F652935A-3BBE-4DCE-AADC-AC1A219A8E11")]
    public interface ITaskItemList : ITaskHost
    {
        /// <summary>
        /// Retrieves the collection of task items from the host object.
        /// </summary>
        /// <returns>
        /// An array of <see cref="ITaskItem"/> objects. Returns an empty array if no items are available.
        /// </returns>
        /// <remarks>
        /// This method is called by MSBuild tasks to retrieve items (credentials, file paths, etc.) passed from the IDE.
        /// </remarks>
        ITaskItem[] GetTaskItems();
    }
}
