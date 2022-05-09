# .NET SDK Workload Preview Bands

In .NET 6, the [SDK Band](https://github.com/dotnet/designs/blob/main/accepted/2020/workloads/workload-manifest.md#sdk-band) (also sometimes called the "SDK feature band") for workloads was defined as the SDK version with the patch number set to 0, and with any prerelease information removed.  For example, the SDK Band would be 6.0.100 for SDKs with version numbers 6.0.100-rc.1.21379.2, 6.0.100, and 6.0.102.

Since different previews used the same SDK band, and workloads by default update to the latest manifests available for the SDK band, this made it easy to get a mismatch between the preview version of the SDK and the version of the workloads that were being used.

Because of this, in .NET 7 we are changing the definition of the SDK band to include the first two components of the SDK version prerelease specifier, if present.  So the SDK band for 7.0.100-preview.1.12345 would be 7.0.100-preview.1, and the SDK band for 7.0.100-rc.2.21505.57 would be 7.0.100-rc.2.

The logic to handle this is primarily in the [SdkFeatureBand class](/src/Resolvers/Microsoft.NET.Sdk.WorkloadManifestReader/SdkFeatureBand.cs).  Additional impacted code is linked in https://github.com/dotnet/sdk/issues/23373.