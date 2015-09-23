// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Hosting
{
    /// <summary>
    /// Defines a free threaded interface for the Vbc task to communicate with the IDE.  In particular,
    /// the Vbc task will delegate the actual compilation to the IDE, rather than shelling
    /// out to the command-line compilers. 
    /// This particular version of Compile (unlike the IVbcHostObject::Compile) is not marshalled back to the UI
    /// thread. The implementor of the interface is responsible for any marshalling.
    /// This was added to allow some of the implementors code to run on the BG thread from which VBC Task is being 
    /// called from.
    /// </summary>
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("ECCF972F-8C2D-4F51-9746-9288661DE2CB")]
    public interface IVbcHostObjectFreeThreaded
    {
        bool Compile();
    }
}
