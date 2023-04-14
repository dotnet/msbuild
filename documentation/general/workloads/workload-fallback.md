# Workload manifest fallback

[.NET SDK Workload Manifests](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workload-manifest.md) describe workloads that are available to be installed in the .NET SDK.  They are produced by each workload, outside of the .NET SDK repos.  A workload manifest has a corresponding [.NET SDK Band](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workload-manifest.md#sdk-band), which is included in the NuGet package ID for the manifest as well as the path it is installed on disk.  Since they are produced in different repos, when the .NET SDK updates to a new band, it's not possible to immediately produce workload manifests with that band.

Because of this, for the workload manifests that are included the .NET SDK, the .NET SDK will fall back to those workload manifests from a previous SDK band.  The IDs of these bundled manifests are listed in an IncludedWorkloadManifests.txt file in the SDK folder.  When looking for manifests, if any of those manifests aren't found for the current feature band, then the SDK will use the corresponding manifest from the most recent feature band that is less than the current SDK version that it finds on disk.

There are currently two issues with the manifest fallback that should be fixed:

- [Workload feature band fallback doesn't work with workload update](https://github.com/dotnet/sdk/issues/23403)
- [Workload feature band fallback doesn't work with rollback files](https://github.com/dotnet/sdk/issues/23402)