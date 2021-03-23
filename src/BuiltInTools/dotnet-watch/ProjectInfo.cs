// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;

namespace Microsoft.DotNet.Watcher
{
    public record ProjectInfo
    (
        string ProjectPath,
        bool IsNetCoreApp,
        Version? TargetFrameworkVersion,
        string RunCommand,
        string RunArguments,
        string RunWorkingDirectory
    )
    {
        private static readonly Version Version3_1 = new Version(3, 1);
        private static readonly Version Version6_0 = new Version(6, 0);

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
