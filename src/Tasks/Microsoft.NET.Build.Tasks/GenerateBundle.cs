// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.Bundle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateBundle : TaskBase
    {
        [Required]
        public ITaskItem[] FilesToBundle { get; set; }
        [Required]
        public string AppHostName { get; set; }
        [Required]
        public bool IncludeSymbols { get; set; }
        [Required]
        public bool IncludeNativeLibraries { get; set; }
        [Required]
        public bool IncludeAllContent { get; set; }
        [Required]
        public string TargetFrameworkVersion { get; set; }
        [Required]
        public string RuntimeIdentifier { get; set; }
        [Required]
        public string OutputDir { get; set; }
        [Required]
        public bool ShowDiagnosticOutput { get; set; }

        [Output]
        public ITaskItem[] ExcludedFiles { get; set; }

        protected override void ExecuteCore()
        {
            OSPlatform targetOS = RuntimeIdentifier.StartsWith("win") ? OSPlatform.Windows :
                                  RuntimeIdentifier.StartsWith("osx") ? OSPlatform.OSX : OSPlatform.Linux;

            BundleOptions options = BundleOptions.None;
            options |= IncludeNativeLibraries ? BundleOptions.BundleNativeBinaries : BundleOptions.None;
            options |= IncludeAllContent ? BundleOptions.BundleAllContent : BundleOptions.None;
            options |= IncludeSymbols ? BundleOptions.BundleSymbolFiles : BundleOptions.None;

            var bundler = new Bundler(AppHostName, OutputDir, options, targetOS, new Version(TargetFrameworkVersion), ShowDiagnosticOutput);

            var fileSpec = new List<FileSpec>(FilesToBundle.Length);

            foreach (var item in FilesToBundle)
            {
                fileSpec.Add(new FileSpec(sourcePath: item.ItemSpec, 
                                          bundleRelativePath:item.GetMetadata(MetadataKeys.RelativePath)));
            }

            bundler.GenerateBundle(fileSpec);

            // Certain files are excluded from the bundle, based on BundleOptions.
            // For example:
            //    Native files and contents files are excluded by default.
            //    hostfxr and hostpolicy are excluded until singlefilehost is available.
            // Return the set of excluded files in ExcludedFiles, so that they can be placed in the publish directory.

            ExcludedFiles = FilesToBundle.Zip(fileSpec, (item, spec) => (spec.Excluded) ? item : null).Where(x => x != null).ToArray();
        }
    }
}
