// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Build
{
    public class DotNetDebTool : DotNetTool
    {
        protected override string Command
        {
            get { return "deb-tool"; }
        }

        protected override string Args
        {
            get { return $"{GetInputDir()} {GetOutputFile()} {GetPackageName()} {GetPackageVersion()}"; }
        }

        [Required]
        public string InputDir { get; set; }

        [Required]
        public string OutputFile { get; set; }

        [Required]
        public string PackageName { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        private string GetInputDir()
        {
            return $"-i {InputDir}";
        }

        private string GetOutputFile()
        {
            return $"-o {OutputFile}";
        }

        private string GetPackageName()
        {
            return $"-n {PackageName}";
        }

        private string GetPackageVersion()
        {
            return $"-v {PackageVersion}";
        }
    }
}
