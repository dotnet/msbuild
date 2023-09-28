// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            List<ITaskItem> knownFrameworkReferences = new();

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

                var knownFrameworkReferencesByWindowsSdkVersion = new Dictionary<Version, List<(Version minimumNetVersion, TaskItem knownFrameworkReference)>>();

                foreach (var supportedWindowsVersion in WindowsSdkSupportedTargetPlatformVersions)
                {
                    var windowsSdkPackageVersion = supportedWindowsVersion.GetMetadata("WindowsSdkPackageVersion");

                    if (!string.IsNullOrEmpty(windowsSdkPackageVersion))
                    {
                        var minimumNETVersion = supportedWindowsVersion.GetMetadata("MinimumNETVersion");
                        Version normalizedMinimumVersion = new(0, 0, 0);
                        if (!string.IsNullOrEmpty(minimumNETVersion))
                        {
                            normalizedMinimumVersion = ProcessFrameworkReferences.NormalizeVersion(new Version(minimumNETVersion));
                            if (normalizedMinimumVersion > normalizedTargetFrameworkVersion)
                            {
                                continue;
                            }
                        }

                        if (!Version.TryParse(supportedWindowsVersion.ItemSpec, out var windowsSdkVersionParsed))
                        {
                            continue;
                        }

                        if (!knownFrameworkReferencesByWindowsSdkVersion.ContainsKey(windowsSdkVersionParsed))
                        {
                            knownFrameworkReferencesByWindowsSdkVersion[windowsSdkVersionParsed] = new();
                        }

                        knownFrameworkReferencesByWindowsSdkVersion[windowsSdkVersionParsed].Add((normalizedMinimumVersion, CreateKnownFrameworkReference(windowsSdkPackageVersion, TargetFrameworkVersion, supportedWindowsVersion.ItemSpec)));
                    }
                }

                foreach (var knownFrameworkReferencesForSdkVersion in knownFrameworkReferencesByWindowsSdkVersion.Values)
                {
                    //  If there are multiple WindowsSdkSupportedTargetPlatformVersion items for the same Windows SDK version, choose the one with the highest minimum version.
                    //  That way it is possible to use older packages when targeting older versions of .NET, and newer packages for newer versions of .NET
                    var highestMinimumVersion = knownFrameworkReferencesForSdkVersion.Max(t => t.minimumNetVersion);
                    knownFrameworkReferences.AddRange(knownFrameworkReferencesForSdkVersion.Where(t => t.minimumNetVersion == highestMinimumVersion).Select(t => t.knownFrameworkReference));
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
