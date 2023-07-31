// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class ResolveStaticWebAssetsEffectiveTargetFramework : Task
    {
        [Required] public ITaskItem [] TargetFrameworks { get; set; }

        [Output] public string EffectiveTargetFramework { get; set; }


        public override bool Execute()
        {
            // We inspect the target frameworks available to find out an appropriate candidate to use for computing the list of
            // static web assets for packing.
            // Target frameworks have the following pattern {FrameworkIdentifier,FrameworkVersion,PlatformIdentifier?,PlatformVersion?}
            // for example: net6.0-windows.10.1980 or net-6.0-ios.14.2.
            // We don't deal with any parsing because that's handled by MSBuild. We also don't care about the platform identifier or the
            // platform version. The reason is explained below.
            // Static web assets don't support having different content based on the target framework. If you are multitargeting (for example,
            // in a Razor Class Library) we consider that all TFMs should produce the same set of static web assets and that we can, as a
            // result, pick any target framework to act as a representative when we are packing the assets into the package.
            // If a package is multitargeting across multiple versions of static web assets, (netstandard2.0 and net6.0 for example) we always
            // use as representative the framework with the highest static web assets version (net6.0 in this case).
            // Platform identifier and version, don't matter because as mentioned above, the list of assets should be identical in those cases
            // and we can pick among any of them to act as a representative for packing purposes.
            var selectedFramework = TargetFrameworks
                .Select(tf => new FrameworkItem(tf))
                .Select(fi => (version: fi.GetStaticWebAssetsVersion(), framework: fi))
                .OrderByDescending(p => p.version)
                .FirstOrDefault(p => p.version > 0);

            EffectiveTargetFramework = selectedFramework.framework?.Moniker;

            return !Log.HasLoggedErrors;
        }


        private class FrameworkItem
        {
            public FrameworkItem(ITaskItem item)
            {
                Moniker = item.ItemSpec;
                TargetFrameworkIdentifier = item.GetMetadata("TargetFrameworkIdentifier");
                TargetFrameworkVersion = item.GetMetadata("TargetFrameworkVersion");
            }

            public string Moniker { get; set; }

            public string TargetFrameworkIdentifier { get; set; }

            public string TargetFrameworkVersion { get; set; }

            internal double GetStaticWebAssetsVersion() =>
                (TargetFrameworkIdentifier, TargetFrameworkVersion) switch
                {
                    (".NETStandard", "2.0") => 1,
                    (".NETStandard", "2.1") => 1,
                    (".NETCoreApp", "3.0") => 1,
                    (".NETCoreApp", "3.1") => 1,
                    // If there is a net5.0 target, prefer that over netstandard because it supports scoped CSS
                    (".NETCoreApp", "5.0") => 1.1,
                    (".NETCoreApp", "6.0") => 2,
                    (".NETCoreApp", "7.0") => 2,
                    // Any future netcoreapp or netstandard will be version 2. If in the future we make additional changes to static web assets
                    // that require a new version, some of our tests will fail to point out we need to change this value.
                    (".NETCoreApp", _) => 2,
                    (".NETStandard", _) => 2,
                    // For anything that we don't know, we return 0. This filters out things like full framework as we won't
                    // consider them.
                    (_, _) => 0,
                };
        }
    }
}
