// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.Bundle;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateBundle : TaskWithAssemblyResolveHooks
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
        [Required]
        public bool EnableCompressionInSingleFile { get; set; }

        [Output]
        public ITaskItem[] ExcludedFiles { get; set; }

        protected override void ExecuteCore()
        {
            OSPlatform targetOS = RuntimeIdentifier.StartsWith("win") ? OSPlatform.Windows :
                                  RuntimeIdentifier.StartsWith("osx") ? OSPlatform.OSX : OSPlatform.Linux;

            Architecture targetArch = RuntimeIdentifier.EndsWith("-x64") || RuntimeIdentifier.Contains("-x64-") ? Architecture.X64 :
                                      RuntimeIdentifier.EndsWith("-x86") || RuntimeIdentifier.Contains("-x86-") ? Architecture.X86 :
                                      RuntimeIdentifier.EndsWith("-arm64") || RuntimeIdentifier.Contains("-arm64-") ? Architecture.Arm64 :
                                      RuntimeIdentifier.EndsWith("-arm") || RuntimeIdentifier.Contains("-arm-") ? Architecture.Arm :
                                      throw new ArgumentException(nameof(RuntimeIdentifier));

            BundleOptions options = BundleOptions.None;
            options |= IncludeNativeLibraries ? BundleOptions.BundleNativeBinaries : BundleOptions.None;
            options |= IncludeAllContent ? BundleOptions.BundleAllContent : BundleOptions.None;
            options |= IncludeSymbols ? BundleOptions.BundleSymbolFiles : BundleOptions.None;
            options |= EnableCompressionInSingleFile ? BundleOptions.EnableCompression : BundleOptions.None;

            Version version = new(TargetFrameworkVersion);
            var bundler = new Bundler(
                AppHostName,
                OutputDir,
                options,
                targetOS,
                targetArch,
                version,
                ShowDiagnosticOutput);

            var fileSpec = new List<FileSpec>(FilesToBundle.Length);

            foreach (var item in FilesToBundle)
            {
                fileSpec.Add(new FileSpec(sourcePath: item.ItemSpec,
                                          bundleRelativePath: item.GetMetadata(MetadataKeys.RelativePath)));
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
