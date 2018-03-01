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
