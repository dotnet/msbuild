// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

namespace NuGet.MSBuildSdkResolver
{
    /// <summary>
    /// Represents a NuGet-based SDK resolver.  It is very important that this class does not reference any NuGet assemblies
    /// directly as an optimization to avoid loading them unless they are needed.  The current implementation only loads
    /// Newtonsoft.Json if a global.json is found and it contains the msbuild-sdks section and a few NuGet assemblies to parse
    /// a version.  The remaining NuGet assemblies are then loaded to do a restore.
    /// </summary>
    public sealed class NuGetSdkResolver : NuGetSdkResolverBase
    {
        public override string Name => nameof(NuGetSdkResolver);

        public override int Priority => 2500;

        protected override SdkResultBase ResolveSdk(SdkReference sdk, SdkResolverContextBase context, SdkResultFactoryBase factory)
        {
            object parsedSdkVersion;

            // This resolver only works if the user specifies a version in a project or a global.json.
            // Ignore invalid versions, there may be another resolver that can handle the version specified
            if (!TryGetNuGetVersionForSdk(sdk.Name, sdk.Version, context, out parsedSdkVersion))
            {
                return null;
            }

            return NuGetAbstraction.GetSdkResult(sdk, parsedSdkVersion, context, factory);
        }

        /// <summary>
        /// Attempts to determine what version of an SDK to resolve.  A project-specific version is used first and then a version specified in a global.json.
        /// This method should not consume any NuGet classes directly to avoid loading additional assemblies when they are not needed.  This method
        /// returns an object so that NuGetVersion is not consumed directly.
        /// </summary>
        internal static bool TryGetNuGetVersionForSdk(string id, string version, SdkResolverContextBase context, out object parsedVersion)
        {
            if (!String.IsNullOrWhiteSpace(version))
            {
                // Use the version specified in the project if it is a NuGet compatible version
                return NuGetAbstraction.TryParseNuGetVersion(version, out parsedVersion);
            }

            Dictionary<string, string> msbuildSdkVersions;

            // Get the SDK versions from a previous state, otherwise find and load global.json to get them

            if (context.State is Dictionary<string, string>)
            {
                msbuildSdkVersions = (Dictionary<string, string>) context.State;
            }
            else
            {
                msbuildSdkVersions = GlobalJsonReader.GetMSBuildSdkVersions(context);

                // Save the SDK versions in case this resolver is called again for another SDK in the same build
                context.State = msbuildSdkVersions;
            }

            string globalJsonVersion;

            // Check if global.json specified a version for this SDK and make sure its a version compatible with NuGet
            if (msbuildSdkVersions != null && msbuildSdkVersions.TryGetValue(id, out globalJsonVersion) && !String.IsNullOrWhiteSpace(globalJsonVersion))
            {
                return NuGetAbstraction.TryParseNuGetVersion(globalJsonVersion, out parsedVersion);
            }

            parsedVersion = null;

            return false;
        }

        /// <summary>
        /// IMPORTANT: This class is used to ensure that <see cref="NuGetSdkResolver"/> does not consume any NuGet classes directly.  This ensures that no NuGet assemblies
        /// are loaded unless they are needed.  Do not implement anything in <see cref="NuGetSdkResolver"/> that uses a NuGet class and instead place it here.
        /// </summary>
        private static class NuGetAbstraction
        {
            public static SdkResultBase GetSdkResult(SdkReference sdk, object nuGetVersion, SdkResolverContextBase context, SdkResultFactoryBase factory)
            {
                // Cast the NuGet version since the caller does not want to consume NuGet classes directly
                NuGetVersion parsedSdkVersion = (NuGetVersion) nuGetVersion;

                // Stores errors and warnings for the result
                ICollection<string> errors = new List<string>();
                ICollection<string> warnings = new List<string>();

                // Load NuGet settings and a path resolver
                ISettings settings = Settings.LoadDefaultSettings(context.ProjectFilePath);

                FallbackPackagePathResolver fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                string installedPath, installedVersion;

                // Attempt to find a package if its already installed
                if (!TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, sdk.Name, parsedSdkVersion, out installedPath, out installedVersion))
                {
                    try
                    {
                        // Asynchronously run the restore without a commit which find the package on configured feeds, download, and unzip it without generating any other files
                        IReadOnlyList<RestoreResultPair> results = RestoreRunnerEx.RunWithoutCommit(
                                context.ProjectFilePath,
                                sdk.Name,
                                parsedSdkVersion.ToFullString(),
                                settings,
                                new NuGetSdkLogger(context.Logger, warnings, errors))
                            .ConfigureAwait(continueOnCapturedContext: false)
                            .GetAwaiter()
                            .GetResult();

                        fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                        // Look for a successful result, any errors are logged by NuGet
                        foreach (RestoreResult result in results.Select(i => i.Result).Where(i => i.Success))
                        {
                            // Find the information about the package that was installed.  In some cases, the version can be different than what was specified (like you specify 1.0 but get 1.0.0)
                            LibraryIdentity installedPackage = result.GetAllInstalled().FirstOrDefault(i => i.Name.Equals(sdk.Name));

                            if (installedPackage != null)
                            {
                                if (!TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, installedPackage.Name, installedPackage.Version, out installedPath, out installedVersion))
                                {
                                    // This should never happen because we were told the package was successfully installed.
                                    // If we can't find it, we probably did something wrong with the NuGet API
                                    errors.Add(ResourceUtilities.FormatResourceString("CouldNotFindInstalledPackage", sdk));
                                }
                            }
                            else
                            {
                                // This should never happen because we were told the restore succeeded.
                                // If we can't find the package from GetAllInstalled(), we probably did something wrong with the NuGet API
                                errors.Add(ResourceUtilities.FormatResourceString("PackageWasNotInstalled", sdk, sdk.Name));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add(e.Message);
                    }
                }

                if (errors.Count == 0)
                {
                    return factory.IndicateSuccess(path: installedPath, version: installedVersion, warnings: warnings);
                }

                return factory.IndicateFailure(errors, warnings);
            }

            /// <summary>
            /// Attempts to parse a string as a NuGetVersion and returns an object containing the instance which can be cast later.
            /// </summary>
            public static bool TryParseNuGetVersion(string version, out object parsed)
            {
                NuGetVersion nuGetVersion;

                if (NuGetVersion.TryParse(version, out nuGetVersion))
                {
                    parsed = nuGetVersion;

                    return true;
                }

                parsed = null;
                return false;
            }

            /// <summary>
            /// Attempts to find a NuGet package if it is already installed.
            /// </summary>
            private static bool TryGetMSBuildSdkPackageInfo(FallbackPackagePathResolver fallbackPackagePathResolver, string id, NuGetVersion version, out string installedPath, out string installedVersion)
            {
                // Find the package
                FallbackPackagePathInfo packageInfo = fallbackPackagePathResolver.GetPackageInfo(id, version);

                if (packageInfo == null)
                {
                    installedPath = null;
                    installedVersion = null;
                    return false;
                }

                // Get the installed path and add the expected "Sdk" folder.  Windows file systems are not case sensitive
                installedPath = Path.Combine(packageInfo.PathResolver.GetInstallPath(packageInfo.Id, packageInfo.Version), "Sdk");

                if (!NativeMethodsShared.IsWindows && !Directory.Exists(installedPath))
                {
                    // Fall back to lower case "sdk" folder in case the file system is case sensitive
                    installedPath = Path.Combine(packageInfo.PathResolver.GetInstallPath(packageInfo.Id, packageInfo.Version), "sdk");
                }

                installedVersion = packageInfo.Version.ToString();

                return true;
            }
        }
    }
}
