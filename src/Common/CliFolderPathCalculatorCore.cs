// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


//only Microsoft.DotNet.MSBuildSdkResolver (net7.0) has nullables enabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable enable
#pragma warning restore IDE0240 // Remove redundant nullable directive

namespace Microsoft.DotNet.Configurer
{
    static class CliFolderPathCalculatorCore
    {
        public const string DotnetHomeVariableName = "DOTNET_CLI_HOME";
        public const string DotnetProfileDirectoryName = ".dotnet";
        public static readonly string PlatformHomeVariableName =
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

        public static string? GetDotnetHomePath()
        {
            var home = Environment.GetEnvironmentVariable(DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable(PlatformHomeVariableName);
                if (string.IsNullOrEmpty(home))
                {
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrEmpty(home))
                    {
                        return null;
                    }
                }
            }

            return home;
        }
    }
}
