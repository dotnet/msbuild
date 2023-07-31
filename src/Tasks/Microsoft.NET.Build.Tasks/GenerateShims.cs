// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.HostModel;
using Microsoft.NET.HostModel.AppHost;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GenerateShims : TaskWithAssemblyResolveHooks
    {
        /// <summary>
        /// Relative paths for Apphost for different ShimRuntimeIdentifiers with RuntimeIdentifier as meta data
        /// </summary>
        [Required]
        public ITaskItem[] ApphostsForShimRuntimeIdentifiers { get; private set; }

        [Required]
        public string IntermediateAssembly { get; set; }

        public string OutputType { get; set; }

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

        private const ushort WindowsGUISubsystem = 0x2;

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

                try
                {
                    var windowsGraphicalUserInterface = runtimeIdentifier.StartsWith("win") && "WinExe".Equals(OutputType, StringComparison.OrdinalIgnoreCase);
                    if (ResourceUpdater.IsSupportedOS() && runtimeIdentifier.StartsWith("win"))
                    {
                        HostWriter.CreateAppHost(appHostSourceFilePath: resolvedApphostAssetPath,
                                                 appHostDestinationFilePath: appHostDestinationFilePath,
                                                 appBinaryFilePath: appBinaryFilePath,
                                                 windowsGraphicalUserInterface: windowsGraphicalUserInterface,
                                                 assemblyToCopyResourcesFrom: IntermediateAssembly);
                    }
                    else
                    {
                        // by passing null to assemblyToCopyResourcesFrom, it will skip copying resources,
                        // which is only supported on Windows
                        if (windowsGraphicalUserInterface)
                        {
                            Log.LogWarning(Strings.AppHostCustomizationRequiresWindowsHostWarning);
                        }

                        HostWriter.CreateAppHost(appHostSourceFilePath: resolvedApphostAssetPath,
                                                 appHostDestinationFilePath: appHostDestinationFilePath,
                                                 appBinaryFilePath: appBinaryFilePath,
                                                 windowsGraphicalUserInterface: false,
                                                 assemblyToCopyResourcesFrom: null);
                    }
                }
                catch (AppNameTooLongException ex)
                {
                    throw new BuildErrorException(Strings.FileNameIsTooLong, ex.LongName);
                }
                catch (PlaceHolderNotFoundInAppHostException ex)
                {
                    throw new BuildErrorException(Strings.AppHostHasBeenModified, resolvedApphostAssetPath, BitConverter.ToString(ex.MissingPattern));
                }

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
