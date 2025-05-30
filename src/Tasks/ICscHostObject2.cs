// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Tasks.Hosting
{
    /// <summary>
    /// Defines an interface for the Csc task to communicate with the IDE.  In particular,
    /// the Csc task will delegate the actual compilation to the IDE, rather than shelling
    /// out to the command-line compilers.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("D6D4E228-259A-4076-B5D0-0627338BCC10")]
    public interface ICscHostObject2 : ICscHostObject
    {
        bool SetWin32Manifest(string win32Manifest);
    }
}
