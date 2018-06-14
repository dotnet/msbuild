using System;
using System.IO;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class WebJobsCommandGenerator
    {
        public static string RunCommand(string targetPath, bool useAppHost, string executableExtension)
        {
            string appName = Path.GetFileName(targetPath);

            string command = $"dotnet {appName}";
            if (useAppHost || string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                command = Path.ChangeExtension(appName, !string.IsNullOrWhiteSpace(executableExtension) ? executableExtension : null);
            }

            return $"{command} %*";
        }
    }
}
