// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Analyzers.UnitTests
{
    public class EndToEndTests
    {
        private readonly TestEnvironment _env;
        public EndToEndTests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);

            // this is needed to ensure the binary logger does not pollute the environment
            _env.WithEnvironmentInvariant();
        }

        [Fact]
        public void SampleAnalyzerIntegrationTest()
        {
            using (TestEnvironment env = TestEnvironment.Create())
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
                TransientTestFolder workFolder = env.CreateFolder(createFolder: true);
                TransientTestFile projectFile = env.CreateFile(workFolder, "FooBar.csproj", contents);
                TransientTestFile projectFile2 = env.CreateFile(workFolder, "FooBar-Copy.csproj", contents2);

                // var cache = new SimpleProjectRootElementCache();
                // ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(projectFile.Path, /*unused*/null, /*unused*/null, cache, false /*Not explicitly loaded - unused*/);


                TransientTestFile config = env.CreateFile(workFolder, "editorconfig.json",
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

                // env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
                env.SetEnvironmentVariable("MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION", "1");
                // string output = RunnerUtilities.ExecMSBuild($"{projectFile.Path} /m:1 -nr:False", out bool success);
                string output = BootstrapRunner.ExecBootstrapedMSBuild($"{projectFile.Path} /m:1 -nr:False -restore", out bool success);
                _env.Output.WriteLine(output);
                success.ShouldBeTrue();
                // The conflicting outputs warning appears
                output.ShouldContain("BC0101");
            }
        }
    }
}
