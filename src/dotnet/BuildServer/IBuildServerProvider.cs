// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.BuildServer
{
    [Flags]
    internal enum ServerEnumerationFlags
    {
        None = 0,
        MSBuild = 1,
        VBCSCompiler = 2,
        Razor = 4,
        All = MSBuild | VBCSCompiler | Razor
    }

    internal interface IBuildServerProvider
    {
        IEnumerable<IBuildServer> EnumerateBuildServers(ServerEnumerationFlags flags = ServerEnumerationFlags.All);
    }
}
