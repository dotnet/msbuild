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
    [Guid("1186fe8f-8aba-48d6-8ce3-32ca42f53728")]
    public interface IVbcHostObject3 : IVbcHostObject2
    {
        bool SetLanguageVersion(string languageVersion);
    }
}
