// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.VisualStudio.Setup.Configuration;

namespace Microsoft.DotNet.Workloads.Workload
{
    /// <summary>
    /// Provides functionality to query the status of .NET workloads in Visual Studio.    
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal static class VisualStudioWorkloads
    {
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        /// <summary>
        /// Visual Studio product ID filters. We dont' want to query SKUs such as Server, TeamExplorer, TestAgent
        /// TestController and BuildTools.
        /// </summary>
        private static readonly string[] s_visualStudioProducts = new string[]
        {
            "Microsoft.VisualStudio.Product.Community",
            "Microsoft.VisualStudio.Product.Professional",
            "Microsoft.VisualStudio.Product.Enterprise",
        };

        /// <summary>
        /// The SWIX package ID wrapping the SDK installer in Visual Studio. The ID should contain
        /// the SDK version as a suffix, e.g., "Microsoft.NetCore.Toolset.5.0.403".
        /// </summary>
        private static readonly string s_visualStudioSdkPackageIdPrefix = "Microsoft.NetCore.Toolset.";

        /// <summary>
        /// Gets a set of workload components based on the available set of workloads for the current SDK.
        /// </summary>
        /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
        /// <returns>A collection of Visual Studio component IDs corresponding to workload IDs.</returns>
        internal static IEnumerable<string> GetAvailableVisualStudioWorkloads(IWorkloadResolver workloadResolver) =>
            workloadResolver.GetAvailableWorkloads().Select(w => w.Id.ToString().Replace('-', '.'));

        /// <summary>
        /// Finds all workloads installed by all Visual Studio instances given that the
        /// SDK installed by an instance matches the feature band of the currently executing SDK.
        /// </summary>
        /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
        /// <param name="installedWorkloads">The collection of installed workloads to update.</param>
        /// <param name="sdkFeatureBand">The feature band of the executing SDK.
        /// If null, then workloads from all feature bands in VS will be returned.
        /// </param>
        internal static void GetInstalledWorkloads(IWorkloadResolver workloadResolver,
            InstalledWorkloadsCollection installedWorkloads, SdkFeatureBand? sdkFeatureBand = null)
        {
            IEnumerable<string> visualStudioWorkloadIds = GetAvailableVisualStudioWorkloads(workloadResolver);
            HashSet<string> installedWorkloadComponents = new();

            // Visual Studio instances contain a large set of packages and we have to perform a linear
            // search to determine whether a matching SDK was installed and look for each installable
            // workload from the SDK. The search is optimized to only scan each set of packages once.
            foreach (ISetupInstance2 instance in GetVisualStudioInstances())
            {
                ISetupPackageReference[] packages = instance.GetPackages();
                bool hasMatchingSdk = false;
                installedWorkloadComponents.Clear();

                for (int i = 0; i < packages.Length; i++)
                {
                    string packageId = packages[i].GetId();

                    if (string.IsNullOrWhiteSpace(packageId))
                    {
                        // Visual Studio already verifies the setup catalog at build time. If the package ID is empty
                        // the catalog is likely corrupted.
                        continue;
                    }

                    if (packageId.StartsWith(s_visualStudioSdkPackageIdPrefix)) // Check if the package owning SDK is installed via VS. Note: if a user checks to add a workload in VS but does not install the SDK, this will cause those workloads to be ignored.
                    {
                        // After trimming the package prefix we should be left with a valid semantic version. If we can't
                        // parse the version we'll skip this instance.
                        if (!ReleaseVersion.TryParse(packageId.Substring(s_visualStudioSdkPackageIdPrefix.Length),
                            out ReleaseVersion visualStudioSdkVersion))
                        {
                            break;
                        }

                        SdkFeatureBand visualStudioSdkFeatureBand = new(visualStudioSdkVersion);

                        // The feature band of the SDK in VS must match that of the SDK on which we're running.
                        if (sdkFeatureBand != null && !visualStudioSdkFeatureBand.Equals(sdkFeatureBand))
                        {
                            break;
                        }

                        hasMatchingSdk = true;

                        continue;
                    }

                    if (visualStudioWorkloadIds.Contains(packageId, StringComparer.OrdinalIgnoreCase))
                    {
                        // Normalize back to an SDK style workload ID.
                        installedWorkloadComponents.Add(packageId.Replace('.', '-'));
                    }
                }

                if (hasMatchingSdk)
                {
                    foreach (string id in installedWorkloadComponents)
                    {
                        installedWorkloads.Add(id, $"VS {instance.GetInstallationVersion()}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of all Visual Studio instances.
        /// </summary>
        /// <returns>A list of Visual Studio instances.</returns>
        private static List<ISetupInstance> GetVisualStudioInstances()
        {
            List<ISetupInstance> vsInstances = new();

            try
            {
                SetupConfiguration setupConfiguration = new();
                ISetupConfiguration2 setupConfiguration2 = setupConfiguration;
                IEnumSetupInstances setupInstances = setupConfiguration2.EnumInstances();
                ISetupInstance[] instances = new ISetupInstance[1];
                int fetched = 0;

                do
                {
                    setupInstances.Next(1, instances, out fetched);

                    if (fetched > 0)
                    {
                        ISetupInstance2 instance = (ISetupInstance2)instances[0];

                        // .NET Workloads only shipped in 17.0 and later and we should only look at IDE based SKUs
                        // such as community, professional, and enterprise.
                        if (Version.TryParse(instance.GetInstallationVersion(), out Version version) &&
                            version.Major >= 17 &&
                            s_visualStudioProducts.Contains(instance.GetProduct().GetId()))
                        {
                            vsInstances.Add(instances[0]);
                        }
                    }
                }
                while (fetched > 0);

            }
            catch (COMException e) when (e.ErrorCode == REGDB_E_CLASSNOTREG)
            {
                // Query API not registered, good indication there are no VS installations of 15.0 or later.
                // Other exceptions are passed through since that likely points to a real error.
            }

            return vsInstances;
        }
    }
}
