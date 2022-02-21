# .NET SDK Workload Rollback

The .NET SDK provides commands that let you list the exact versions of workloads installed, or to install a specific version of the workloads.  These commands are currently not documented in the command-line help or in the online documentation, as they are not intended for mainline scenarios and may not work in some cases.  This document describes these commands and how they work.

## Show current workload manifest versions

The following command will print the currently installed workload manifest versions:

```
dotnet workload update --print-rollback
```

The output will list each workload manifest ID and the corresponding version, in the following format:

```
==workloadRollbackDefinitionJsonOutputStart==
{"microsoft.net.sdk.android":"31.0.101-preview.11.89","microsoft.net.sdk.ios":"15.0.101-preview.11.445","microsoft.net.sdk.maccatalyst":"15.0.101-preview.11.445","microsoft.net.sdk.macos":"12.0.101-preview.11.445","microsoft.net.sdk.maui":"6.0.101-preview.11.2159","microsoft.net.sdk.tvos":"15.0.101-preview.11.445","microsoft.net.workload.emscripten":"6.0.0","microsoft.net.workload.mono.toolchain":"6.0.0"}
==workloadRollbackDefinitionJsonOutputEnd==
```

Note that all of the workload manifests will be listed, whether or not any workloads are installed or not.

## Install a specific version of workloads

To install a specific version of workloads, you need a file using the same JSON format as the `dotnet workload update --print-rollback` command outputs (without the start/end header/footer lines).  Then you can install those versions of the workloads with the following command:

```
dotnet workload update --from-rollback-file <ROLLBACK_FILE>
```

The `ROLLBACK_FILE` value can either be a path to a file on disk or a URL where the file will be downloaded.

After updating the workload manifests to the specified versions, the command will upgrade or downgrade any installed workloads to the versions specified in the updated workload manifests.

It is possible to construct a rollback file manually, or to specify a subset of workload manifests in a rollback file that is used to update the workload versions.  However, since workloads can and do use packs that are defined in multiple manifest files, this could result in using versions of workload packs that otherwise would not have been used together and may not be compatible.  For this reason, it's recommended to always specify the full set of workload manifests in the rollback file, and to use versions that were shipped together or are known to be compatible with each other.