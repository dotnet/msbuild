# User-local workloads

A .NET SDK distribution can opt in to user-local workload installation.  If enabled, workloads will be installed in a folder under a user-specific folder rather than in the `dotnet` installation folder.  This is used for the source-built version of .NET, which will often be installed by a package manager in a folder that requires root permissions to write to.  With user-local workload installation enabled, workloads can be installed without root premission, and without polluting the dotnet install folder.

To opt in to user-local workload installation, a distribution can include an empty `/metadata/workloads/<sdkfeatureband>/userlocal` file under the dotnet root installation folder.  If this file exists, then workloads will be installed under a "user" or "home" folder.  This folder is calculated as follows: If a `DOTNET_CLI_HOME` environment variable is set, then use that.  Otherwise, use the `USERPROFILE` environment variable on Windows, and the `HOME` environment variable elsewhere.  In all three of these cases, `.dotnet` is then appended to the folder.  This `.dotnet` folder is then used instead of the root `dotnet` folder for workload paths.  For example, updated manifests will be under the `sdk-manifests` folder, and workload packs will be installed under the `packs` folder.

Note that the `<sdkfeatureband>` portion of the marker file path does NOT include any preview specifier, in contrast to how the feature band is now calculated for most other workload operations.  For example, the marker file path would be `/metadata/workloads/7.0.100/userlocal` for all of the following SDK versions: `7.0.100-preview.2`, `7.0.100-rc.1`, `7.0.100`, `7.0.102`.

Original issue: https://github.com/dotnet/sdk/issues/18104
PR: https://github.com/dotnet/sdk/pull/18823
Marker file PR: https://github.com/dotnet/installer/pull/12021