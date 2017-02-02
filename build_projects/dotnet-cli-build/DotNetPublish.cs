// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetPublish : DotNetMSBuildTool
    {
        protected override string Command
        {
            get { return "publish"; }
        }

        protected override string Args
        {
            get { return $"{base.Args} {GetProjectPath()} {GetConfiguration()} {GetFramework()} {GetNativeSubdirectory()} {GetBuildBasePath()} {GetOutput()} {GetVersionSuffix()} {GetRuntime()} {GetMSBuildArgs()}"; }
        }

        public string BuildBasePath { get; set; }

        public string Configuration { get; set; }

        public string Framework { get; set; }

        public bool NativeSubDirectory { get; set; }

        public string MSBuildArgs { get; set; }

        public string Output { get; set; }

        public string ProjectPath { get; set; }

        public string Runtime { get; set; }

        public string VersionSuffix { get; set; }

        private string GetBuildBasePath()
        {
            if (!string.IsNullOrEmpty(BuildBasePath))
            {
                return $"--build-base-path {BuildBasePath}";
            }

            return null;
        }

        private string GetConfiguration()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"--configuration {Configuration}";
            }

            return null;
        }

        private string GetFramework()
        {
            if (!string.IsNullOrEmpty(Framework))
            {
                return $"--framework {Framework}";
            }

            return null;
        }

        private string GetNativeSubdirectory()
        {
            if (NativeSubDirectory)
            {
                return $"--native-subdirectory";
            }

            return null;
        }

        private string GetMSBuildArgs()
        {
            if (!string.IsNullOrEmpty(MSBuildArgs))
            {
                return $"{MSBuildArgs}";
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

        private string GetRuntime()
        {
            if (!string.IsNullOrEmpty(Runtime))
            {
                return $"--runtime {Runtime}";
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
    }
}
