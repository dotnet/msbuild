using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class CrossgenDirectory : Task
    {
        [Required]
        public string CoreCLRVersion { get; set; }

        [Required]
        public string JitVersion { get; set; }

        [Required]
        public string SharedFrameworkNameVersionPath { get; set; }

        [Required]
        public string SdkOutputDirectory { get; set; }        

        public override bool Execute()
        {
            var crossgenUtil = new Crossgen(DependencyVersions.CoreCLRVersion, DependencyVersions.JitVersion);

            crossgenUtil.CrossgenDirectory(SharedFrameworkNameVersionPath, SdkOutputDirectory);

            return true;
        }
    }
}
