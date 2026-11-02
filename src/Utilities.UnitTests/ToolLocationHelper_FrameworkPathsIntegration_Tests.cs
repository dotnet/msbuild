// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed class ToolLocationHelper_FrameworkPathsIntegration_Tests
{
#if FEATURE_CODETASKFACTORY
    private readonly ITestOutputHelper _output;
#endif

    public ToolLocationHelper_FrameworkPathsIntegration_Tests(ITestOutputHelper output)
    {
#if FEATURE_CODETASKFACTORY
        _output = output;
#endif

        ToolLocationHelper.ClearStaticCaches();
    }

    [Fact]
    public void TestGetPathToBuildToolsFile()
    {
        string net20Path = ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version20);

        net20Path?.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "2.0"));

        string net35Path = ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version35);

        net35Path?.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "3.5"));

        ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version40).ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "4.0"));

        string tv12path = Path.Combine(ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion).ToolsPath, Constants.MSBuildExecutableName);

        tv12path.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, ObjectModelHelpers.MSBuildDefaultToolsVersion));
        tv12path.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, ToolLocationHelper.CurrentToolsVersion));
    }

#if RUNTIME_TYPE_NETCORE
    [Fact(Skip = "https://github.com/dotnet/msbuild/issues/722")]
#else
    [Fact]
#endif
    public void TestGetPathToBuildToolsFile_32Bit()
    {
        string net20Path = ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version20, DotNetFrameworkArchitecture.Bitness32);
        net20Path?.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "2.0", DotNetFrameworkArchitecture.Bitness32));

        string net35Path = ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version35, DotNetFrameworkArchitecture.Bitness32);
        net35Path?.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "3.5", DotNetFrameworkArchitecture.Bitness32));

        ToolLocationHelper.GetPathToDotNetFrameworkFile(Constants.MSBuildExecutableName, TargetDotNetFrameworkVersion.Version40, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, "4.0", DotNetFrameworkArchitecture.Bitness32));

        var toolsPath32 = ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion).Properties["MSBuildToolsPath32"];
        string tv12path = Path.Combine(Path.GetFullPath(toolsPath32.EvaluatedValue), Constants.MSBuildExecutableName);

        tv12path.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, ObjectModelHelpers.MSBuildDefaultToolsVersion, DotNetFrameworkArchitecture.Bitness32));
        tv12path.ShouldBe(ToolLocationHelper.GetPathToBuildToolsFile(Constants.MSBuildExecutableName, ToolLocationHelper.CurrentToolsVersion, DotNetFrameworkArchitecture.Bitness32));
    }

#if FEATURE_CODETASKFACTORY
    private const string VerifyToolsetAndToolLocationHelperProjectCommonContent = """
        string currentInstallFolderLocation = null;

        using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows"))
        {
            if (baseKey != null)
            {
                object keyValue = baseKey.GetValue("CurrentInstallFolder");

                if (keyValue != null)
                {
                    currentInstallFolderLocation = keyValue.ToString();
                }
            }
        }

        string sdk35ToolsPath = Sdk35ToolsPath == null ? Sdk35ToolsPath : Path.GetFullPath(Sdk35ToolsPath);
        string sdk40ToolsPath = Sdk40ToolsPath == null ? Sdk40ToolsPath : Path.GetFullPath(Sdk40ToolsPath);
        pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.GetFullPath(pathTo35Sdk);
        pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.GetFullPath(pathTo40Sdk);
        string currentInstall35Location = null;
        string currentInstall40Location = null;

        if (currentInstallFolderLocation != null)
        {
            currentInstall35Location = Path.GetFullPath(Path.Combine(currentInstallFolderLocation, "bin\\"));
            currentInstall40Location = Path.GetFullPath(Path.Combine(currentInstallFolderLocation, "bin\\NetFX 4.0 Tools\\"));
        }

        Log.LogMessage(MessageImportance.High, "SDK35ToolsPath           = {0}", Sdk35ToolsPath);
        Log.LogMessage(MessageImportance.High, "SDK40ToolsPath           = {0}", Sdk40ToolsPath);
        Log.LogMessage(MessageImportance.High, "pathTo35Sdk              = {0}", pathTo35Sdk);
        Log.LogMessage(MessageImportance.High, "pathTo40Sdk              = {0}", pathTo40Sdk);
        Log.LogMessage(MessageImportance.High, "currentInstall35Location = {0}", currentInstall35Location);
        Log.LogMessage(MessageImportance.High, "currentInstall40Location = {0}", currentInstall40Location);

        if (!String.Equals(sdk35ToolsPath, pathTo35Sdk, StringComparison.OrdinalIgnoreCase) &&
            (currentInstall35Location != null &&  /* this will be null on win8 express since 35 tools and this registry key will not be written, for vsultimate it is written*/
            !String.Equals(currentInstall35Location, pathTo35Sdk, StringComparison.OrdinalIgnoreCase))
            )
        {
            Log.LogError("Sdk35ToolsPath is incorrect! Registry: {0}  ToolLocationHelper: {1}  CurrentInstallFolder: {2}", sdk35ToolsPath, pathTo35Sdk, currentInstall35Location);
        }

        if (!String.Equals(sdk40ToolsPath, pathTo40Sdk, StringComparison.OrdinalIgnoreCase) &&
            (currentInstall40Location != null &&  /* this will be null on win8 express since 35 tools and this registry key will not be written, for vsultimate it is written*/
            !String.Equals(currentInstall40Location, pathTo40Sdk, StringComparison.OrdinalIgnoreCase))
            )
        {
            Log.LogError("Sdk40ToolsPath is incorrect! Registry: {0}  ToolLocationHelper: {1}  CurrentInstallFolder: {2}", sdk40ToolsPath, pathTo40Sdk, currentInstall40Location);
        }
    """;

    [Fact(Skip = "https://github.com/dotnet/msbuild/issues/995")]
    public void VerifyToolsetAndToolLocationHelperAgree()
    {
        string projectContents = ObjectModelHelpers.CleanupFileContents($$"""
            <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                    <ParameterGroup>
                        <Sdk35ToolsPath />
                        <Sdk40ToolsPath />
                        <WindowsSDK80Path />
                    </ParameterGroup>
                    <Task>
                        <Using Namespace='Microsoft.Win32'/>
                        <Code>
                        <![CDATA[
                            string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("gacutil.exe", TargetDotNetFrameworkVersion.Version35);
                            if (!String.IsNullOrEmpty(pathTo35Sdk))
                            {
                                pathTo35Sdk = Path.GetDirectoryName(pathTo35Sdk) + "\\";
                            }

                            string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("gacutil.exe", TargetDotNetFrameworkVersion.VersionLatest);

                            if (!String.IsNullOrEmpty(pathTo40Sdk))
                            {
                                pathTo40Sdk = Path.GetDirectoryName(pathTo40Sdk) + "\\";
                            }

                            string pathTo81WinSDK = ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.VersionLatest, VisualStudioVersion.VersionLatest);
                            {{VerifyToolsetAndToolLocationHelperProjectCommonContent}}
                            if (!String.Equals(WindowsSDK80Path, pathTo81WinSDK, StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogError("WindowsSDK80Path is incorrect! Registry: {0}  ToolLocationHelper: {1}", WindowsSDK80Path, pathTo81WinSDK);
                            }

                            return !Log.HasLoggedErrors;
                        ]]>
                        </Code>
                    </Task>
                </UsingTask>
                <Target Name='Build'>
                    <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                </Target>
            </Project>
            """);

        ILogger logger = new MockLogger(_output);
        using ProjectCollection collection = new ProjectCollection();
        Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, logger);

        bool success = p.Build(logger);

        success.ShouldBeTrue(); // "Build Failed.  See Std Out for details."
    }

    [Fact]
    public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIsEmpty()
    {
        string projectContents = $"""
            <Project ToolsVersion='4.0'>
                <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                    <ParameterGroup>
                        <Sdk35ToolsPath />
                        <Sdk40ToolsPath />
                        <WindowsSDK80Path />
                    </ParameterGroup>
                    <Task>
                        <Using Namespace='Microsoft.Win32'/>
                        <Code>
                        <![CDATA[
                            string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35);
                            string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40);

                            pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, "bin\\");
                            pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, "bin\\NetFX 4.0 Tools\\");{VerifyToolsetAndToolLocationHelperProjectCommonContent}return !Log.HasLoggedErrors;
                        ]]>
                        </Code>
                    </Task>
                </UsingTask>
                <Target Name='Build'>
                    <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                </Target>
            </Project>
            """;

        ILogger logger = new MockLogger(_output);

        using ProjectCollection collection = new ProjectCollection();
        Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, "4.0", logger);

        bool success = p.Build(logger);

        success.ShouldBeTrue(); // "Build Failed.  See Std Out for details."
    }

    [Fact]
    public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIs10()
    {
        string projectContents = $"""
            <Project ToolsVersion='4.0'>
                <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                    <ParameterGroup>
                        <Sdk35ToolsPath />
                        <Sdk40ToolsPath />
                        <WindowsSDK80Path />
                    </ParameterGroup>
                    <Task>
                        <Using Namespace='Microsoft.Win32'/>
                        <Code>
                        <![CDATA[
                            string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.Version100);
                            string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100);

                            pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, "bin\\");
                            pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, "bin\\NetFX 4.0 Tools\\");
                            {VerifyToolsetAndToolLocationHelperProjectCommonContent}
                            return !Log.HasLoggedErrors;
                        ]]>
                        </Code>
                    </Task>
                </UsingTask>
                <Target Name='Build'>
                    <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                </Target>
            </Project>
            """;

        ILogger logger = new MockLogger(_output);
        IDictionary<string, string> globalProperties = new Dictionary<string, string>();
        globalProperties.Add("VisualStudioVersion", "10.0");

        using ProjectCollection collection = new ProjectCollection(globalProperties);
        Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, "4.0", logger);

        bool success = p.Build(logger);

        success.ShouldBeTrue(); // "Build Failed.  See Std Out for details."
    }

    [WindowsOnlyFact]
    public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIs11()
    {
        string projectContents = $$"""
            <Project ToolsVersion='4.0'>
                <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                    <ParameterGroup>
                        <Sdk35ToolsPath />
                        <Sdk40ToolsPath />
                        <WindowsSDK80Path />
                    </ParameterGroup>
                    <Task>
                        <Using Namespace='Microsoft.Win32'/>
                        <Code>
                        <![CDATA[
                            string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.Version110);
                            string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110);
                            string pathTo80WinSDK = ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110);

                            pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, "bin\\");
                            pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, "bin\\NetFX 4.0 Tools\\");
                            {{VerifyToolsetAndToolLocationHelperProjectCommonContent}}
                            if (String.IsNullOrEmpty(WindowsSDK80Path))
                            {
                                Log.LogWarning("WindowsSDK80Path is empty, which is technically not correct, but we're letting it slide for now because the OTG build won't have the updated registry for a while.  Make sure we don't see this warning on PURITs runs, though!");
                            }
                            else if (!String.Equals(WindowsSDK80Path, pathTo80WinSDK, StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogError("WindowsSDK80Path is incorrect! Registry: {0}  ToolLocationHelper: {1}", WindowsSDK80Path, pathTo80WinSDK);
                            }

                            return !Log.HasLoggedErrors;
                        ]]>
                        </Code>
                    </Task>
                </UsingTask>
                <Target Name='Build'>
                    <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                </Target>
            </Project>
            """;

        ILogger logger = new MockLogger(_output);
        IDictionary<string, string> globalProperties = new Dictionary<string, string>
        {
            ["VisualStudioVersion"] = "11.0",
        };

        using ProjectCollection collection = new(globalProperties);
        Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, "4.0", logger);

        bool success = p.Build(logger);

        success.ShouldBeTrue(); // "Build Failed.  See Std Out for details."
    }
#endif
}
