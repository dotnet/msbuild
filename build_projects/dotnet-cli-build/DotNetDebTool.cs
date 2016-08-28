// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
        public string InputDirectory { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string PackageName { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        private string GetInputDir()
        {
            return $"-i {InputDirectory}";
        }

        private string GetOutputFile()
        {
            return $"-o {OutputDirectory}";
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
