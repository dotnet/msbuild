// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class WebJobsCommandGenerator
    {
        public static string RunCommand(string targetPath, bool useAppHost, string executableExtension)
        {
            string appName = Path.GetFileName(targetPath);

            string command = $"dotnet {appName}";
            if (useAppHost)
            {
                command = Path.ChangeExtension(appName, !string.IsNullOrWhiteSpace(executableExtension) ? executableExtension : null);
            }

            // For Apps targeting .NET Framework, the extension is always exe. RID is not set for .NETFramework apps with PlatformType set to AnyCPU.
            if (string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                command = Path.ChangeExtension(appName, ".exe");
            }

            return $"{command} %*";
        }
    }
}
