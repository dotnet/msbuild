// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Hosting
{
    /*
     * Interface:       ICscHostObject4
     *
     * Defines an interface for the Csc task to communicate with the IDE.  In particular,
     * the Csc task will delegate the actual compilation to the IDE, rather than shelling
     * out to the command-line compilers.
     *
     */
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("0DDB496F-C93C-492C-87F1-90B6FDBAA833")]
    public interface ICscHostObject4 : ICscHostObject3
    {
        bool SetPlatformWith32BitPreference(string platformWith32BitPreference);
        bool SetHighEntropyVA(bool highEntropyVA);
        bool SetSubsystemVersion(string subsystemVersion);
    }
}
