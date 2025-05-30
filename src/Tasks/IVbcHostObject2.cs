// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Tasks.Hosting
{
    /// <summary>
    /// Defines an interface for the Vbc task to communicate with the IDE.  In particular,
    /// the Vbc task will delegate the actual compilation to the IDE, rather than shelling
    /// out to the command-line compilers.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("f59afc84-d102-48b1-a090-1b90c79d3e09")]
    public interface IVbcHostObject2 : IVbcHostObject
    {
        bool SetOptionInfer(bool optionInfer);
        bool SetModuleAssemblyName(string moduleAssemblyName);
        bool SetWin32Manifest(string win32Manifest);
    }
}
