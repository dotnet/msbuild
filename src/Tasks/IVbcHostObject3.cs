// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

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
