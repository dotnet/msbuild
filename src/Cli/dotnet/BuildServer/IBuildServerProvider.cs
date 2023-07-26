// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
