// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class EndToEndTests : IDisposable
{
    private readonly TestEnvironment _env;
    public EndToEndTests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);

        // this is needed to ensure the binary logger does not pollute the environment
        _env.WithEnvironmentInvariant();
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public void PropertiesUsageAnalyzerTest()
    {
        using TestEnvironment env = TestEnvironment.Create();
        string contents = """
                              <Project DefaultTargets="PrintEnvVar">

                              <PropertyGroup>
                              <!--
                              <MyProp1>value-of-prop1</MyProp1>
                              <MyProp2>$(MyProp1)</MyProp2>
                              <MyProp3>blah</MyProp3>
                              -->
                              </PropertyGroup>
                              
                              
                              <PropertyGroup Condition="'$(MyProp12)' == ''">
                                <MyProp13>$(MyProp11)</MyProp13>
                              </PropertyGroup>
                              
                              
                              <!--
                              <ItemGroup>
                                <a Include="$(nonexistent)" />
                              </ItemGroup>
                              -->

                              <Target Name="PrintEnvVar">
                              <Message Text="MyPropT2 has value $(MyPropT2)" Importance="High" Condition="'$(MyPropT2)' == ''" />
                              <PropertyGroup>
                              <MyPropT2>$(MyPropT2);xxx</MyPropT2>
                              </PropertyGroup>
                              </Target>

                              </Project>
                              """;
        TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
        TransientTestFile projectFile = env.CreateFile(logFolder, "myProj.proj", contents);

        string output = RunnerUtilities.ExecBootstrapedMSBuild($"{projectFile.Path} -analyze", out bool success);
        _env.Output.WriteLine(output);
        _env.Output.WriteLine("=========================");
        success.ShouldBeTrue(output);

        output.ShouldContain("AB001: Property: [MyProp11]");
        output.ShouldContain("AB002: Property: [MyPropT2]");
        output.ShouldContain("AB003: Property: [MyProp13]");

        // each finding should be found just once - but reported twice, due to summary
        Regex.Matches(output, "AB001: Property").Count.ShouldBe(2);
        Regex.Matches(output, "AB002: Property").Count.ShouldBe(2);
        Regex.Matches(output, "AB003: Property").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SampleAnalyzerIntegrationTest(bool buildInOutOfProcessNode, bool analysisRequested)
    {
        string contents = $"""
            <Project Sdk="Microsoft.NET.Sdk" DefaultTargets="Hello">
                
                <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                </PropertyGroup>
                  
                <PropertyGroup Condition="$(Test) == true">
                <TestProperty>Test</TestProperty>
                </PropertyGroup>
                 
                <ItemGroup>
                <ProjectReference Include=".\FooBar-Copy.csproj" />
                </ItemGroup>
                  
                <Target Name="Hello">
                <Message Importance="High" Condition="$(Test2) == true" Text="XYZABC" />
                </Target>
                
            </Project>
            """;

        string contents2 = $"""
            <Project Sdk="Microsoft.NET.Sdk">
                               
                <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                </PropertyGroup>
                                 
                <PropertyGroup Condition="$(Test) == true">
                <TestProperty>Test</TestProperty>
                </PropertyGroup>
                                
                <ItemGroup>
                <Reference Include="bin/foo.dll" />
                </ItemGroup>
                                
                <Target Name="Hello">
                <Message Importance="High" Condition="$(Test2) == true" Text="XYZABC" />
                </Target>
                               
            </Project>
            """;
        TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
        TransientTestFile projectFile = _env.CreateFile(workFolder, "FooBar.csproj", contents);
        TransientTestFile projectFile2 = _env.CreateFile(workFolder, "FooBar-Copy.csproj", contents2);

        // var cache = new SimpleProjectRootElementCache();
        // ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(projectFile.Path, /*unused*/null, /*unused*/null, cache, false /*Not explicitly loaded - unused*/);


        TransientTestFile config = _env.CreateFile(workFolder, "editorconfig.json",
            /*lang=json,strict*/
            """
            {
                "BC0101": {
                    "IsEnabled": true,
                    "Severity": "Error"
                },
                "COND0543": {
                    "IsEnabled": false,
                    "Severity": "Error",
                    "EvaluationAnalysisScope": "AnalyzedProjectOnly",
                    "CustomSwitch": "QWERTY"
                },
                "BLA": {
                    "IsEnabled": false
                }
            }
            """);

        // OSX links /var into /private, which makes Path.GetTempPath() return "/var..." but Directory.GetCurrentDirectory return "/private/var...".
        // This discrepancy breaks path equality checks in analyzers if we pass to MSBuild full path to the initial project.
        // See if there is a way of fixing it in the engine - tracked: https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=55702688.
        _env.SetCurrentDirectory(Path.GetDirectoryName(projectFile.Path));

        _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", buildInOutOfProcessNode ? "1" : "0");
        _env.SetEnvironmentVariable("MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION", "1");
        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"{Path.GetFileName(projectFile.Path)} /m:1 -nr:False -restore" +
            (analysisRequested ? " -analyze" : string.Empty), out bool success, false, _env.Output);
        _env.Output.WriteLine(output);
        success.ShouldBeTrue();
        // The conflicting outputs warning appears - but only if analysis was requested
        if (analysisRequested)
        {
            output.ShouldContain("BC0101");
        }
        else
        {
            output.ShouldNotContain("BC0101");
        }
    }
}
