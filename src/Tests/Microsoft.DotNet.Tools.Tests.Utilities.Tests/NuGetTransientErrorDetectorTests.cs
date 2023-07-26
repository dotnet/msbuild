// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Tests.Utilities.Tests
{
    public class NuGetTransientErrorDetectorTests
    {
        [Fact]
        public void Error1()
        {
            string input =
                "[31;1m    Microsoft.NET.ToolPack.Tests.GivenThatWeWantToPackAToolProjectWithPackagedShim.It_contains_shim" +
                "(multiTarget: True, targetFramework: \"netcoreapp2.1\") [FAIL]\r\n\u001B[m\u001B[37m      Expected command to" +
                " pass but it did not.\r\n\u001B[m\u001B[37m      File Name: /datadisks/disk1/work/AD2E0974/p/d/dotnet\r\n\u001B[m\u001B[37m" +
                "      Arguments: msbuild /t:Pack /datadisks/disk1/work/AD2E0974/w/A09F08FB/e/testExecutionDirectory/NupkgOfPackWi---4E69F8CE/" +
                "consoledemo.csproj /restore\r\n\u001B[m\u001B[37m      Exit Code: 1\r\n\u001B[m\u001B[37m      StdOut:\r\n\u001B[m\u001B[37m      " +
                "Microsoft (R) Build Engine version 16.10.0-preview-21126-01+6819f7ab0 for .NET\r\n\u001B[m\u001B[37m      Copyright (C) Microsoft " +
                "Corporation. All rights reserved.\r\n\u001B[m\u001B[37m      \r\n\u001B[m\u001B[37m        Determining projects to restore...\r\n\u001B" +
                "[m\u001B[37m        Retrying 'FindPackagesByIdAsync' for source 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/" +
                "_packaging/1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft.netcore.dotnethostpolicy/index.json'.\r\n\u001B[m" +
                "\u001B[37m        Response status code does not indicate success: 503 (Service Unavailable).\r\n\u001B[m\u001B[37m        " +
                "Retrying 'FindPackagesByIdAsync' for source 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/" +
                "1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft.netcore.dotnethostpolicy/index.json'.\r\n\u001B[m\u001B" +
                "[37m        Response status code does not indicate success: 503 (Service Unavailable).\r\n\u001B[m\u001B[37m      " +
                "/datadisks/disk1/work/AD2E0974/p/d/sdk/6.0.100-ci/NuGet.targets(131,5): error : Failed to retrieve information about" +
                " 'runtime.osx-x64.Microsoft.NETCore.DotNetHostPolicy' from remote source 'https://pkgs.dev.azure.com/dnceng/" +
                "9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft." +
                "netcore.dotnethostpolicy/index.json'. [/datadisks/disk1/work/AD2E0974/w/A09F08FB/e/testExecutionDirectory/NupkgOfPackWi---4E69F8CE" +
                "/consoledemo.csproj]\r\n\u001B[m\u001B[37m      /datadisks/disk1/work/AD2E0974/p/d/sdk/6.0.100-ci/NuGet.targets(131,5): error :   " +
                "Response status code does not indicate success: 503 (Service Unavailable). [/datadisks/disk1/work/AD2E0974/w/A09F08FB/e/" +
                "testExecutionDirectory/NupkgOfPackWi---4E69F8CE/consoledemo.csproj]\r\n\u001B[m\u001B[37m      StdErr:\r\n\u001B[m\u001B[37m      " +
                "\r\n\u001B[m\u001B[37m      \r\n\u001B[m\u001B[30;1m      Stack Trace:\r\n\u001B[m\u001B[37m           at " +
                "FluentAssertions.Execution.XUnit2TestFramework.Throw(String message)\r\n\u001B[m\u001B[37m           at " +
                "FluentAssertions.Execution.TestFrameworkProvider.Throw(String message)\r\n\u001B[m\u001B[37m           at " +
                "FluentAssertions.Execution.DefaultAssertionStrategy.HandleFailure(String message)\r\n\u001B[m\u001B[37m           at " +
                "FluentAssertions.Execution.AssertionScope.FailWith(String message, Object[] args)\r\n\u001B[m\u001B[37m        " +
                "/_/src/Tests/Microsoft.NET.TestFramework/Assertions/CommandResultAssertions.cs(32,0): at Microsoft.NET.TestFramework.Assertions." +
                "CommandResultAssertions.Pass()\r\n\u001B[m\u001B[37m        /_/src/Tests/Microsoft.NET.ToolPack.Tests/" +
                "PackWithShimsAndResultNugetPackageNuGetPackagexFixture.cs(64,0): at Microsoft.NET.ToolPack.Tests.NupkgOfPackWithShimsFixture." +
                "SetupNuGetPackage(Boolean multiTarget, String targetFramework)\r\n\u001B[mRunning /datadisks/disk1/work/AD2E0974/p/d/" +
                "dotnet msbuild /t:Pack /datadisks/disk1/work/AD2E0974/w/A09F08FB/e/testExecutionDirectory/NupkgOfPackWi---BDB2B0FB/consoledemo.csproj " +
                "/restore\r\n\u001B[37m        /_/src/Tests/Microsoft.NET.ToolPack.Tests/PackWithShimsAndResultNugetPackageNuGetPackagexFixture.cs(47,0): " +
                "at Microsoft.NET.ToolPack.Tests.NupkgOfPackWithShimsFixture.GetTestToolPackagePath(Boolean multiTarget, String targetFramework)" +
                "\r\n\u001B[m\u001B[37m        /_/src/Tests/Microsoft.NET.ToolPack.Tests/GivenThatWeWantToPackAToolProjectWithPackagedShim.cs(128,0): " +
                "at Microsoft.NET.ToolPack.Tests.GivenThatWeWantToPackAToolProjectWithPackagedShim.It_contains_shim(Boolean multiTarget, String " +
                "targetFramework)\r\n\u001B[m\u001B[30;1m      Output:\r\n\u001B[m\u001B[37m        > /datadisks/disk1/work/AD2E0974/p/d/dotnet msbuild " +
                "/t:Pack /datadisks/disk1/work/AD2E0974/w/A09F08FB/e/testExecutionDirectory/NupkgOfPackWi---4E69F8CE/consoledemo.csproj /restore" +
                "\r\n\u001B[m\u001B[37m        Microsoft (R) Build Engine version 16.10.0-preview-21126-01+6819f7ab0 for .NET\r\n\u001B[m\u001B[37m        " +
                "Copyright (C) Microsoft Corporation. All rights reserved.\r\n\u001B[m\u001B[37m        \r\n\u001B[m\u001B[37m          Determining " +
                "projects to restore...\r\n\u001B[m\u001B[37m          Retrying 'FindPackagesByIdAsync' for source 'https://pkgs.dev.azure.com/dnceng/" +
                "9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft.netcore." +
                "dotnethostpolicy/index.json'.\r\n\u001B[m\u001B[37m          Response status code does not indicate success: 503 (Service Unavailable)." +
                "\r\n\u001B[m\u001B[37m          Retrying 'FindPackagesByIdAsync' for source 'https://pkgs.dev.azure.com/dnceng/" +
                "9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft.netcore." +
                "dotnethostpolicy/index.json'.\r\n\u001B[m\u001B[37m          Response status code does not indicate success: 503 (Service Unavailable)" +
                ".\r\n\u001B[m\u001B[37m        /datadisks/disk1/work/AD2E0974/p/d/sdk/6.0.100-ci/NuGet.targets(131,5): error : Failed to retrieve " +
                "information about 'runtime.osx-x64.Microsoft.NETCore.DotNetHostPolicy' from remote source 'https://pkgs.dev.azure.com/dnceng/" +
                "9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/1a5f89f6-d8da-4080-b15f-242650c914a8/nuget/v3/flat2/runtime.osx-x64.microsoft.netcore." +
                "dotnethostpolicy/index.json'. [/datadisks/disk1/work/AD2E0974/w/A09F08FB/e/testExecutionDirectory/NupkgOfPackWi---4E69F8CE/" +
                "consoledemo.csproj]\r\n\u001B[m\u001B[37m        /datadisks/disk1/work/AD2E0974/p/d/sdk/6.0.100-ci/NuGet.targets(131,5): error :   " +
                "Response status code does not indicate success: 503 (Service Unavailable). [/datadisks/disk1/work/AD2E0974/w/A09F08FB/e/" +
                "testExecutionDirectory/NupkgOfPackWi---4E69F8CE/consoledemo.csproj]\r\n\u001B[m\u001B[37m        Exit Code: 1";
            NuGetTransientErrorDetector.IsTransientError(input).Should().BeTrue();
        }

        [Fact]
        public void Error2()
        {
            string input =
                "C:\\h\\w\\BA6F09CC\\t\\dotnetSdkTests\\raba4db1.z5b\\compose_depen---2E9E4DDB\\FluentAssertion.xml\" (Restore target) (1) ->\r\n      " +
                "(Restore target) -> \r\n        C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\Common7\\IDE\\CommonExtensions\\Microsoft" +
                "\\NuGet\\NuGet.targets(131,5): error : Failed to retrieve information about 'runtime.win.System.Console' from remote source " +
                "'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/c9f8ac11-6bd8-4926-8306-f075241547f7/nuget/v3" +
                "/flat2/runtime.win.system.console/index.json'. [C:\\h\\w\\BA6F09CC\\t\\dotnetSdkTests\\raba4db1.z5b\\compose_depen---2E9E4DDB\\" +
                "FluentAssertion.xml]\r\n      C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Preview\\Common7\\IDE\\CommonExtensions\\" +
                "Microsoft\\NuGet\\NuGet.targets(131,5): error :   An error occurred while sending the request. [C:\\h\\w\\BA6F09CC\\t\\dotnetSdkTests" +
                "\\raba4db1.z5b\\compose_depen---2E9E4DDB\\FluentAssertion.xml]\r\n      C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\" +
                "Preview\\Common7\\IDE\\CommonExtensions\\Microsoft\\NuGet\\NuGet.targets(131,5): error :   " +
                "The request was aborted: The request was canceled. [C:\\h\\w\\BA6F09CC\\t\\dotnetSdkTests\\raba4db1.z5b\\compose_depen---2E9E4DDB" +
                "\\FluentAssertion.xml]\r\n";
            NuGetTransientErrorDetector.IsTransientError(input).Should().BeTrue();
        }

        [Fact]
        public void Error3()
        {
            string input =
                @"Determining projects to restore...
        Failed to download package 'runtime.win-x64.Microsoft.NETCore.DotNetHostResolver.2.1.24' from 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/45bacae2-5efb-47c8-91e5-8ec20c22b4f8/nuget/v3/flat2/runtime.win-x64.microsoft.netcore.dotnethostresolver/2.1.24/runtime.win-x64.microsoft.netcore.dotnethostresolver.2.1.24.nupkg'.
        A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. (k0ivsblobprodcus356.vsblob.vsassets.io:443)
          A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.
        Failed to download package 'runtime.win-x64.Microsoft.NETCore.DotNetHostResolver.2.1.24' from 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/45bacae2-5efb-47c8-91e5-8ec20c22b4f8/nuget/v3/flat2/runtime.win-x64.microsoft.netcore.dotnethostresolver/2.1.24/runtime.win-x64.microsoft.netcore.dotnethostresolver.2.1.24.nupkg'.
        A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. (k0ivsblobprodcus356.vsblob.vsassets.io:443)
          A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.
      drive\NuGet.targets(131,5): error : Failed to download package 'runtime.win-x64.Microsoft.NETCore.DotNetHostResolver.2.1.24' from 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/45bacae2-5efb-47c8-91e5-8ec20c22b4f8/nuget/v3/flat2/runtime.win-x64.microsoft.netcore.dotnethostresolver/2.1.24/runtime.win-x64.microsoft.netcore.dotnethostresolver.2.1.24.nupkg'. [C:\h\w\B79F0A31\t\dotnetSdkTests\zvrozon1.u4m\RunFromOutput---802469DB\RunFromOutputFolderWithRID_netcoreapp2.1\RunFromOutputFolderWithRID_netcoreapp2.1.csproj]
      drive\NuGet.targets(131,5): error : A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. (k0ivsblobprodcus356.vsblob.vsassets.io:443) [C:\h\w\B79F0A31\t\dotnetSdkTests\zvrozon1.u4m\RunFromOutput---802469DB\RunFromOutputFolderWithRID_netcoreapp2.1\RunFromOutputFolderWithRID_netcoreapp2.1.csproj]
      drive\NuGet.targets(131,5): error :   A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond. [C:\h\w\B79F0A31\t\dotnetSdkTests\zvrozon1.u4m\RunFromOutput---802469DB\RunFromOutputFolderWithRID_netcoreapp2.1\RunFromOutputFolderWithRID_netcoreapp2.1.csproj]
      drive\NuGet.targets(131,5): error : The feed 'dotnet-public [https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json]' lists package 'runtime.win-x64.Microsoft.NETCore.DotNetHostResolver.2.1.24' but multiple attempts to download the nupkg have failed. The feed is either invalid or required packages were removed while the current operation was in progress. Verify the package exists on the feed and try again. [C:\h\w\B79F0A31\t\dotnetSdkTests\zvrozon1.u4m\RunFromOutput---802469DB\RunFromOutputFolderWithRID_netcoreapp2.1\RunFromOutputFolderWithRID_netcoreapp2.1.csproj]
      drive\NuGet.targets(131,5): error :   Unable to find package 'runtime.win-x64.Microsoft.NETCore.DotNetHostResolver.2.1.24'. [C:\h\w\B79F0A31\t\dotnetSdkTests\zvrozon1.u4m\RunFromOutput---802469DB\RunFromOutputFolderWithRID_netcoreapp2.1\RunFromOutputFolderWithRID_netcoreapp2.1.csproj]";
            NuGetTransientErrorDetector.IsTransientError(input).Should().BeTrue();
        }

        [Fact]
        public void NoTransientError()
        {
            string input =
                @"
info : Adding PackageReference for package 'Newtonsoft.Json' into project 'console/console.csproj'.
info : Restoring packages for console/console.csproj...
info : Package 'Newtonsoft.Json' is compatible with all the specified frameworks in project 'console/console.csproj'.
info : Committing restore...
info : Generating MSBuild file console/obj/console.csproj.nuget.g.props.
info : Generating MSBuild file console/obj/console.csproj.nuget.g.targets.
info : Writing assets file to disk. Path: console/obj/project.assets.json
log  : Restored console/console.csproj (in 120 ms).

Build started 3/10/2021 1:53:51 PM.
     1>Project  on node 1 (Restore target(s)).
     1>_GetAllRestoreProjectPathItems:
         Determining projects to restore...
       Restore:
         Restoring packages for console/console.csproj...
         Committing restore...
         Generating MSBuild file console/obj/console.csproj.nuget.g.props.
         Generating MSBuild file console/obj/console.csproj.nuget.g.targets.
         Writing assets file to disk. Path: console/obj/project.assets.json
         Restored console/console.csproj (in 131 ms).
         
         NuGet Config files used:
             /.nuget/NuGet/NuGet.Config
         
         Feeds used:
             https://api.nuget.org/v3/index.json";
            NuGetTransientErrorDetector.IsTransientError(input).Should().BeFalse();
        }
    }
}
