// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Hosting
{
    /// <summary>
    /// Defines an interface for the Csc task to communicate with the IDE.  In particular,
    /// the Csc task will delegate the actual compilation to the IDE, rather than shelling
    /// out to the command-line compilers.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("F9353662-F1ED-4a23-A323-5F5047E85F5D")]
    public interface ICscHostObject3 : ICscHostObject2
    {
        bool SetApplicationConfiguration(string applicationConfiguration);
    }
}
