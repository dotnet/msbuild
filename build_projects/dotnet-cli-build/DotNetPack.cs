// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetPack : DotNetMSBuildTool
    {
        protected override string Command
        {
            get { return "pack"; }
        }

        protected override string Args
        {
            get { return $"{base.Args} {GetProjectPath()} {GetConfiguration()} {GetNoBuild()} {GetOutput()} {GetVersionSuffix()} {GetRuntime()} {GetIncludeSymbols()} {MsbuildArgs}"; }
        }

        public string Configuration { get; set; }

        public bool NoBuild { get; set; }

        public string MsbuildArgs { get; set; }

        public string Output { get; set; }

        public string ProjectPath { get; set; }

        public string VersionSuffix { get; set; }

        public string Runtime { get; set; }

        public bool IncludeSymbols { get; set; }

        private string GetConfiguration()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"--configuration {Configuration}";
            }

            return null;
        }

        private string GetNoBuild()
        {
            if (NoBuild)
            {
                return $"--no-build";
            }

            return null;
        }

        private string GetOutput()
        {
            if (!string.IsNullOrEmpty(Output))
            {
                return $"--output {Output}";
            }

            return null;
        }

        private string GetProjectPath()
        {
            if (!string.IsNullOrEmpty(ProjectPath))
            {
                return $"{ProjectPath}";
            }

            return null;
        }

        private string GetVersionSuffix()
        {
            if (!string.IsNullOrEmpty(VersionSuffix))
            {
                return $"--version-suffix {VersionSuffix}";
            }

            return null;
        }

        private string GetRuntime()
        {
            if (!string.IsNullOrEmpty(Runtime))
            {
                return $"-property:RuntimeIdentifier={Runtime}";
            }

            return null;
        }

        private string GetIncludeSymbols()
        {
            if (IncludeSymbols)
            {
                return $"--include-symbols";
            }

            return null;
        }

    }
}
