// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher
{
    internal sealed record ProjectInfo
    (
        string ProjectPath,
        bool IsNetCoreApp,
        Version? TargetFrameworkVersion,
        string RuntimeIdentifier,
        string DefaultAppHostRuntimeIdentifier,
        string RunCommand,
        string RunArguments,
        string RunWorkingDirectory
    )
    {
        private static readonly Version Version3_1 = new(3, 1);
        private static readonly Version Version6_0 = new(6, 0);

        public bool IsNetCoreApp31OrNewer()
        {
            return IsNetCoreApp && TargetFrameworkVersion is not null && TargetFrameworkVersion >= Version3_1;
        }

        public bool IsNetCoreApp60OrNewer()
        {
            return IsNetCoreApp && TargetFrameworkVersion is not null && TargetFrameworkVersion >= Version6_0;
        }
    }
}
