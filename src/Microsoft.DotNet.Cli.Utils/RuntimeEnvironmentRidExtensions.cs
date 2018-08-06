// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    // This is to support some legacy stuff.
    // dnu restore (and thus dotnet restore) always uses win7-x64 as the Windows restore target,
    // so, when picking targets out of the lock file, we need to do version fallback since the
    // active RID might be higher than the RID in the lock file.
    //
    // We should clean this up. Filed #619 to track.
    public static class RuntimeEnvironmentRidExtensions
    {
        // Gets the identifier that is used for restore by default (this is different from the actual RID, but only on Windows)
        public static string GetLegacyRestoreRuntimeIdentifier()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                return RuntimeEnvironment.GetRuntimeIdentifier();
            }
            else
            {
                var arch = RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
                return "win7-" + arch;
            }
        }
    }
}
