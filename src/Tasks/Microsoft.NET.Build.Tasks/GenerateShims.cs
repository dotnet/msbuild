// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GenerateShims : TaskBase
    {
        /// <summary>
        /// Relative paths for Apphost for different ShimRuntimeIdentifiers with RuntimeIdentifier as meta data
        /// </summary>
        [Required]
        public ITaskItem[] ApphostsForShimRuntimeIdentifiers { get; private set; }

        /// <summary>
        /// PackageId of the dotnet tool NuGet Package.
        /// </summary>
        [Required]
        public string PackageId { get; set; }

        /// <summary>
        /// Package Version of the dotnet tool NuGet Package.
        /// </summary>
        [Required]
        public string PackageVersion { get; set; }

        /// <summary>
        /// TFM to use for compile-time assets.
        /// </summary>
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        /// <summary>
        /// The command name of the dotnet tool.
        /// </summary>
        [Required]
        public string ToolCommandName { get; set; }

        /// <summary>
        /// The entry point of the dotnet tool which will be run by Apphost
        /// </summary>
        [Required]
        public string ToolEntryPoint { get; set; }

        /// <summary>
        /// The output directory path of generated shims.
        /// </summary>
        [Required]
        public string PackagedShimOutputDirectory { get; set; }

        /// <summary>
        /// The RuntimeIdentifiers that shims will be generated for.
        /// </summary>
        [Required]
        public ITaskItem[] ShimRuntimeIdentifiers { get; set; }

        /// <summary>
        /// Path of generated shims. metadata "ShimRuntimeIdentifier" is used to map back to input ShimRuntimeIdentifiers.
        /// </summary>
        [Output]
        public ITaskItem[] EmbeddedApphostPaths { get; private set; }

        protected override void ExecuteCore()
        {
            var embeddedApphostPaths = new List<ITaskItem>();
            foreach (var runtimeIdentifier in ShimRuntimeIdentifiers.Select(r => r.ItemSpec))
            {
                var resolvedApphostAssetPath = GetApphostAsset(ApphostsForShimRuntimeIdentifiers, runtimeIdentifier);

                var packagedShimOutputDirectoryAndRid = Path.Combine(
                        PackagedShimOutputDirectory,
                        runtimeIdentifier);

                var appHostDestinationFilePath = Path.Combine(
                        packagedShimOutputDirectoryAndRid,
                        ToolCommandName + ExecutableExtension.ForRuntimeIdentifier(runtimeIdentifier));

                Directory.CreateDirectory(packagedShimOutputDirectoryAndRid);

                // per https://github.com/dotnet/cli/issues/9870 nuget layout (as in {packageid}/{packageversion}/tools/)is normalized version
                var normalizedPackageVersion = NuGetVersion.Parse(PackageVersion).ToNormalizedString();
                // This is the embedded string. We should normalize it on forward slash, so the file won't be different according to
                // build machine.
                var appBinaryFilePath = string.Join("/",
                    new[] {
                        ".store",
                        PackageId.ToLowerInvariant(),
                        normalizedPackageVersion,
                        PackageId.ToLowerInvariant(),
                        normalizedPackageVersion,
                        "tools",
                        NuGetUtils.ParseFrameworkName(TargetFrameworkMoniker).GetShortFolderName(),
                        "any",
                        ToolEntryPoint});

                AppHost.Create(
                    resolvedApphostAssetPath,
                    appHostDestinationFilePath,
                    appBinaryFilePath
                );

                var item = new TaskItem(appHostDestinationFilePath);
                item.SetMetadata(MetadataKeys.ShimRuntimeIdentifier, runtimeIdentifier);
                embeddedApphostPaths.Add(item);
            }

            EmbeddedApphostPaths = embeddedApphostPaths.ToArray();
        }

        private string GetApphostAsset(ITaskItem[] apphostsForShimRuntimeIdentifiers, string runtimeIdentifier)
        {
            return apphostsForShimRuntimeIdentifiers.Single(i => i.GetMetadata(MetadataKeys.RuntimeIdentifier) == runtimeIdentifier).ItemSpec;
        }
    }
}
