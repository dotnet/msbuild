// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    //  Generate KnownFrameworkReference items for the Windows SDK pack
    //  If WindowsSdkPackageVersion is set, then use that for the package version of the Windows SDK pack
    //  Otherwise, if UseWindowsSDKPreview is set, then construct the package version from the TargetPlatformVersion (dropping the 4th component of the component and appending "-preview")
    //  Otherwise, create KnownFrameworkReference items based on WindowsSdkSupportedTargetPlatformVersion items, using WindowsSdkPackageVersion and MinimumNETVersion metadata

    public class CreateWindowsSdkKnownFrameworkReferences : TaskBase
    {
        public bool UseWindowsSDKPreview { get; set; }

        public string WindowsSdkPackageVersion { get; set; }

        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public string TargetPlatformIdentifier { get; set; }

        public string TargetPlatformVersion { get; set; }

        public ITaskItem[] WindowsSdkSupportedTargetPlatformVersions { get; set; }

        [Output]
        public ITaskItem[] KnownFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            List<ITaskItem> knownFrameworkReferences = new List<ITaskItem>();

            if (!string.IsNullOrEmpty(WindowsSdkPackageVersion))
            {
                knownFrameworkReferences.Add(CreateKnownFrameworkReference(WindowsSdkPackageVersion, TargetFrameworkVersion, TargetPlatformVersion));
            }
            else if (UseWindowsSDKPreview)
            {
                var tpv = new Version(TargetPlatformVersion);

                var windowsSdkPackageVersion = $"{tpv.Major}.{tpv.Minor}.{tpv.Build}-preview";

                knownFrameworkReferences.Add(CreateKnownFrameworkReference(windowsSdkPackageVersion, TargetFrameworkVersion, TargetPlatformVersion));
            }
            else
            {
                var normalizedTargetFrameworkVersion = ProcessFrameworkReferences.NormalizeVersion(new Version(TargetFrameworkVersion));
                foreach (var supportedWindowsVersion in WindowsSdkSupportedTargetPlatformVersions)
                {
                    var windowsSdkPackageVersion = supportedWindowsVersion.GetMetadata("WindowsSdkPackageVersion");

                    if (!string.IsNullOrEmpty(windowsSdkPackageVersion))
                    {
                        var minimumNETVersion = supportedWindowsVersion.GetMetadata("MinimumNETVersion");
                        if (!string.IsNullOrEmpty(minimumNETVersion))
                        {
                            var normalizedMinimumVersion = ProcessFrameworkReferences.NormalizeVersion(new Version(minimumNETVersion));
                            if (normalizedMinimumVersion > normalizedTargetFrameworkVersion)
                            {
                                continue;
                            }
                        }

                        knownFrameworkReferences.Add(CreateKnownFrameworkReference(windowsSdkPackageVersion, TargetFrameworkVersion, supportedWindowsVersion.ItemSpec));
                    }
                }
            }

            KnownFrameworkReferences = knownFrameworkReferences.ToArray();
        }

        private static TaskItem CreateKnownFrameworkReference(string windowsSdkPackageVersion, string targetFrameworkVersion, string targetPlatformVersion)
        {
            var knownFrameworkReference = new TaskItem("Microsoft.Windows.SDK.NET.Ref");
            knownFrameworkReference.SetMetadata("TargetFramework", $"net{targetFrameworkVersion}-windows{targetPlatformVersion}");
            knownFrameworkReference.SetMetadata("RuntimeFrameworkName", "Microsoft.Windows.SDK.NET.Ref");
            knownFrameworkReference.SetMetadata("DefaultRuntimeFrameworkVersion", windowsSdkPackageVersion);
            knownFrameworkReference.SetMetadata("LatestRuntimeFrameworkVersion", windowsSdkPackageVersion);
            knownFrameworkReference.SetMetadata("TargetingPackName", "Microsoft.Windows.SDK.NET.Ref");
            knownFrameworkReference.SetMetadata("TargetingPackVersion", windowsSdkPackageVersion);
            knownFrameworkReference.SetMetadata("RuntimePackAlwaysCopyLocal", "true");
            knownFrameworkReference.SetMetadata("RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref");
            knownFrameworkReference.SetMetadata("RuntimePackRuntimeIdentifiers", "any");
            knownFrameworkReference.SetMetadata("IsWindowsOnly", "true");

            return knownFrameworkReference;
        }
    }
}
