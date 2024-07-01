## Quick Intro
The document describes the logic behind the bootstrap and testing capabilities for the fresh MSBuild bits.

## History
MSBuild is built for two different environments: .NET and .NET Framework. To check the changes for .NET, the fresh bits were published to the MSBuild.Bootstrap folder and copied to the bootstrap later together with a set of specific dependencies to make it work as a part of the .dotnet folder.

## Current Implementation for .NET
During the bootstrap phase, install-scripts is used for downloading the bits that are compatible with the current version. The logic of interplay with the scripts is moved to a separate MSBuild task: InstallDotNetCoreTask.cs. What happens under the hood:

 1. The SDK is downloaded in the bootstrap folder.
 2. Fresh MSBuild bits are copied to it later.
 3. The constructed SDK is used for testing for both: local e2e tests and CI runs.