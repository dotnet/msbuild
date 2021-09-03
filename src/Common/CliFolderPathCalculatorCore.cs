// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

#nullable enable

namespace Microsoft.DotNet.Configurer
{
    static class CliFolderPathCalculatorCore
    {
        public const string DotnetHomeVariableName = "DOTNET_CLI_HOME";
        public const string DotnetProfileDirectoryName = ".dotnet";

        public static string PlatformHomeVariableName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";

        public static string? GetDotnetUserProfileFolderPath()
        {
            string? homePath = GetDotnetHomePath();
            if (homePath is null)
            {
                return null;
            }

            return Path.Combine(homePath, DotnetProfileDirectoryName);
        }

        public static string? GetDotnetHomePath(Func<string, string?>? getEnvironmentVariable = null)
        {
            getEnvironmentVariable ??= key => Environment.GetEnvironmentVariable(key);

            var home = getEnvironmentVariable(DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                home = getEnvironmentVariable(PlatformHomeVariableName);
                if (string.IsNullOrEmpty(home))
                {
                    return null;
                }
            }

            return home;
        }
    }
}
