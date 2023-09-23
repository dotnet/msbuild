// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class WebConfigTelemetry
    {
        // An example of a project line looks like this:
        //  Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{05A5AD00-71B5-4612-AF2F-9EA9121C4111}"
        private static readonly Lazy<Regex> s_crackProjectLine = new(
            () => new Regex
                (
                @"^" // Beginning of line
                + @"Project\s*\(\""[^""]*?\""\)"
                + @"\s*=\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + @"""[^""]*?"""
                + @"\s*,\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + @"\""(?<RELATIVEPATH>[^""]*?)\"""
                + @"\s*,\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + @"\""(?<PROJECTGUID>[^""]*?)\"""
                + @"\s*$", // End-of-line
                RegexOptions.Compiled
                )
            );

        public static XDocument AddTelemetry(XDocument webConfig, string projectGuid, bool ignoreProjectGuid, string solutionFileFullPath, string projectFileFullPath)
        {
            try
            {
                bool isCLIOptOutEnabled = EnvironmentHelper.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault);
                if (string.IsNullOrEmpty(projectGuid) && !ignoreProjectGuid && !isCLIOptOutEnabled)
                {
                    projectGuid = GetProjectGuidFromSolutionFile(solutionFileFullPath, projectFileFullPath);
                }

                // Add the projectGuid to web.config if it is not present. Remove ProjectGuid from web.config if opted out.
                webConfig = WebConfigTransform.AddProjectGuidToWebConfig(webConfig, projectGuid, ignoreProjectGuid);
            }
            catch
            {
                // Telemtry
            }

            return webConfig;
        }

        public static string GetProjectGuidFromSolutionFile(string solutionFileFullPath, string projectFileFullPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(solutionFileFullPath) && File.Exists(solutionFileFullPath))
                {
                    return GetProjectGuid(solutionFileFullPath, projectFileFullPath);
                }

                int parentLevelsToSearch = 5;
                string solutionDirectory = Path.GetDirectoryName(projectFileFullPath);

                while (parentLevelsToSearch-- > 0)
                {
                    if (string.IsNullOrEmpty(solutionDirectory) || !Directory.Exists(solutionDirectory))
                    {
                        return null;
                    }

                    IEnumerable<string> solutionFiles = Directory.EnumerateFiles(solutionDirectory, "*.sln", SearchOption.TopDirectoryOnly);
                    foreach (string solutionFile in solutionFiles)
                    {
                        string projectGuid = GetProjectGuid(solutionFile, projectFileFullPath);
                        if (!string.IsNullOrEmpty(projectGuid))
                        {
                            return projectGuid;
                        }
                    }

                    solutionDirectory = Directory.GetParent(solutionDirectory)?.FullName;
                }
            }
            catch
            {
                // This code path is only used for telemetry.
            }

            return null;
        }

        private static string GetProjectGuid(string solutionFileFullPath, string projectFileFullPath)
        {
            if (!string.IsNullOrEmpty(solutionFileFullPath) && File.Exists(solutionFileFullPath))
            {
                string[] solutionFileLines = File.ReadAllLines(solutionFileFullPath);
                foreach (string solutionFileLine in solutionFileLines)
                {
                    Match match = s_crackProjectLine.Value.Match(solutionFileLine);
                    if (match.Success)
                    {
                        string projectRelativePath = match.Groups["RELATIVEPATH"].Value.Trim();
                        string projectFullPathConstructed = Path.Combine(Path.GetDirectoryName(solutionFileFullPath), projectRelativePath);
                        projectFullPathConstructed = Path.GetFullPath((new Uri(projectFullPathConstructed)).LocalPath);
                        if (string.Equals(projectFileFullPath, projectFullPathConstructed, StringComparison.OrdinalIgnoreCase))
                        {
                            string projectGuid = match.Groups["PROJECTGUID"].Value.Trim();
                            return projectGuid;
                        }
                    }
                }
            }

            return null;
        }
    }
}
