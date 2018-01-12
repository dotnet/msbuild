// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Utilities;
#if !FEATURE_APPDOMAIN
using System.Runtime.Loader;
#endif

using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;

namespace NuGet.MSBuildSdkResolver
{
    /// <summary>
    /// Acts as a base class for the NuGet-based SDK resolver and handles assembly resolution to dynamically locate NuGet assemblies.
    /// </summary>
    public abstract class NuGetSdkResolverBase : SdkResolverBase
    {
        /// <summary>
        /// The sub-folder under the Visual Studio installation where the NuGet assemblies are located.
        /// </summary>
        public const string PathToNuGetUnderVisualStudioRoot = @"Common7\IDE\CommonExtensions\Microsoft\NuGet";

        /// <summary>
        /// Attempts to locate the NuGet assemblies based on the current <see cref="BuildEnvironmentMode"/>.
        /// </summary>
        private static readonly Lazy<string> NuGetAssemblyPathLazy = new Lazy<string>(() =>
        {
            // The environment variable overrides everything
            string basePath = Environment.GetEnvironmentVariable(MSBuildConstants.NuGetAssemblyPathEnvironmentVariableName);

            if (!String.IsNullOrWhiteSpace(basePath) && Directory.Exists(basePath))
            {
                return basePath;
            }

            if (BuildEnvironmentHelper.Instance.Mode == BuildEnvironmentMode.VisualStudio)
            {
                // Return the path to NuGet under the Visual Studio installation
                return Path.Combine(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory, PathToNuGetUnderVisualStudioRoot);
            }

            // Expect the NuGet assemblies to be next to MSBuild.exe, which is the case when running .NET CLI
            return BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32;
        });

        /// <summary>
        /// A list of NuGet assemblies that we have a dependency on but should load at runtime.  This list is from dependencies of the
        /// NuGet.Commands and NuGet.Protocol packages in project.json.  This list should be updated if those dependencies change.
        /// </summary>
        private static readonly HashSet<string> NuGetAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Newtonsoft.Json",
            "NuGet.Commands",
            "NuGet.Common",
            "NuGet.Configuration",
            "NuGet.Frameworks",
            "NuGet.LibraryModel",
            "NuGet.Packaging",
            "NuGet.ProjectModel",
            "NuGet.ProjectModel",
            "NuGet.Protocol",
            "NuGet.Versioning",
        };

        static NuGetSdkResolverBase()
        {
            if (!Traits.Instance.EscapeHatches.DisableNuGetSdkResolver)
            {
#if FEATURE_APPDOMAIN
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
#else
                AssemblyLoadContext.Default.Resolving += AssemblyResolve;
#endif
            }
        }

        /// <summary>
        /// A custom assembly resolver used to locate NuGet dependencies.  It is very important that we do not ship with
        /// these dependencies because we need to load whatever version of NuGet is currently installed.  If we loaded our
        /// own NuGet assemblies, it would break NuGet functionality like Restore and Pack.
        /// </summary>
        private static Assembly AssemblyResolve(
#if FEATURE_APPDOMAIN
            object sender,
            ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
#else
            AssemblyLoadContext assemblyLoadContext,
            AssemblyName assemblyName)
        {
#endif
            if (NuGetAssemblies.Contains(assemblyName.Name))
            {
                string assemblyPath = Path.Combine(NuGetAssemblyPathLazy.Value, $"{assemblyName.Name}.dll");

                if (File.Exists(assemblyPath))
                {
#if !FEATURE_APPDOMAIN
                    return assemblyLoadContext.LoadFromAssemblyPath(assemblyPath);
#elif !CLR2COMPATIBILITY
                    return Assembly.UnsafeLoadFrom(assemblyPath);
#else
                    return Assembly.LoadFrom(assemblyPath);
#endif
                }
            }

            return null;
        }
    }
}
