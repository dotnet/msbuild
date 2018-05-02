Update various SDKs that we bundle with mono.

The versions for the SDKs are obtained from some props files in the `cli` repo, based on
a particular commit hash. And that commit hash is from the branch that is being tracked,
eg. `release/2.1.1xx` branch corresponding to msbuild's `vs15.6` branch.

The scripts don't remove the existing files first, so that has to be done manually:

	$ rm -Rf sdks/Microsoft.NET.Sdk* sdks/NuGet.Build.Tasks.Pack/ sdks/FSharp.NET.Sdk/
	$ rm -Rf nuget-support/tasks-targets/ nuget-support/tv/ mono/ExtensionsPath/Microsoft/Microsoft.NET.Build.Extensions/
	
Usage:
	$ msbuild mono/build/build.proj /p:CLICommitHash=<cli_commit_hash>

Also, whenever this is updated, please add the Microsoft.NET.Build.Extensions version in mono's
`tools/nuget-hash-extractor/download.sh` and update the denied lists.

MSBuildSdkResolver also needs to be updated in sync with the SDKs. This has two parts:

    1. libhostfxr*: native library. We can get the nuget version for this and can fetch that given a
       `$(HostMonikerRid)` like `osx-x64`.

    2. The resolver assembly itself which is distributed as part of the CLI sdk nuget, but we can't
       reliably get the version for that, given a cli commit hash. So, for now we build `cli` repo locally
       and just copy over the assembly.

       Note: Currently they use commit count to get the full version, but we can't depend on that. And
       even this version can be overridden when builds are generated.
