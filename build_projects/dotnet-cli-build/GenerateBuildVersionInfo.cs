// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateBuildVersionInfo : ToolTask
    {
        [Required]
        public string RepoRoot { get; set; }

        [Required]
        public int VersionMajor { get; set; }

        [Required]
        public int VersionMinor { get; set; }

        [Required]
        public int VersionPatch { get; set; }

        [Required]
        public string ReleaseSuffix { get; set; }

        [Output]
        public string CommitCount { get; set; }

        [Output]
        public string VersionSuffix { get; set; }

        [Output]
        public string SimpleVersion { get; set; }

        [Output]
        public string NugetVersion { get; set; }

        [Output]
        public string MsiVersion { get; set; }

        [Output]
        public string VersionBadgeMoniker { get; set; }

        private int _commitCount;

        public override bool Execute()
        {
            base.Execute();

            var buildVersion = new BuildVersion()
            {
                Major = VersionMajor,
                Minor = VersionMinor,
                Patch = VersionPatch,
                ReleaseSuffix = ReleaseSuffix,
                CommitCount = _commitCount
            };

            CommitCount = buildVersion.CommitCountString;
            VersionSuffix = buildVersion.VersionSuffix;
            SimpleVersion = buildVersion.SimpleVersion;
            NugetVersion = buildVersion.NuGetVersion;
            MsiVersion = buildVersion.GenerateMsiVersion();
            VersionBadgeMoniker = Monikers.GetBadgeMoniker();

            return true;
        }

        protected override string ToolName
        {
            get { return "git"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            // Workaround: https://github.com/Microsoft/msbuild/issues/1215
            // There's a "git" folder on the PATH in VS 2017 Developer command prompt and it causes msbuild to fail to execute git.
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "git.exe" : "git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"rev-list --count HEAD";
        }

        protected override void LogEventsFromTextOutput(string line, MessageImportance importance)
        {
            _commitCount = int.Parse(line);
        }
    }
}