// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
#pragma warning disable xUnit1004 // Test methods should not be skipped

    public class GivenThatWeWantToTargetNet471 : SdkTest
    {
        public GivenThatWeWantToTargetNet471(ITestOutputHelper log) : base(log)
        {
        }

        string[] net471Shims =
        {
            "System.Data.Common.dll",
            "System.Diagnostics.StackTrace.dll",
            "System.Diagnostics.Tracing.dll",
            "System.Globalization.Extensions.dll",
            "System.IO.Compression.dll",
            "System.Net.Http.dll",
            "System.Net.Sockets.dll",
            "System.Runtime.Serialization.Primitives.dll",
            "System.Security.Cryptography.Algorithms.dll",
            "System.Security.SecureString.dll",
            "System.Threading.Overlapped.dll",
            "System.Xml.XPath.XDocument.dll"
        };

        [WindowsOnlyFact]
        public void It_builds_a_net471_app()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App",
                TargetFrameworks = "net471",
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.exe.config",
                $"{testProject.Name}.pdb",
            });
        }

        [WindowsOnlyFact]
        public void It_builds_a_net471_app_referencing_netstandard20()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard20",
                TargetFrameworks = "net471",
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name = "NetStandard20_Library",
                TargetFrameworks = "netstandard2.0",
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns20");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",
                $"{testProject.Name}.exe.config", // We have now added binding redirects so we should expect a config flag to be dropped to the output directory.
            }.Concat(net471Shims));
        }

        [WindowsOnlyFact]
        public void It_does_not_include_facades_from_nuget_packages()
        {
            var testProject = new TestProject()
            {
                Name = "Net471_NuGetFacades",
                TargetFrameworks = "net471",
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("NETStandard.Library", "1.6.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.exe.config",
                $"{testProject.Name}.pdb",
                
                // These two will be included because Netstandard1.x has a higher version of these two contracts than net4.7.1 which is why they will be added.
                "System.Net.Http.dll",
                "System.IO.Compression.dll",

                //  This is an implementation dependency of the System.Net.Http package, which won't get conflict resolved out
                "System.Diagnostics.DiagnosticSource.dll",
            });
        }

        [WindowsOnlyFact]
        public void It_includes_shims_when_net471_app_references_netstandard16()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard16",
                TargetFrameworks = "net471",
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name = "NetStandard16_Library",
                TargetFrameworks = "netstandard1.6",
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns16");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",
                $"{testProject.Name}.exe.config", // We have now added binding redirects so we should expect a config flag to be dropped to the output directory.
                "System.Diagnostics.DiagnosticSource.dll" //  This is an implementation dependency of the System.Net.Http package, which won't get conflict resolved out
            }.Concat(net471Shims));
        }

        [WindowsOnlyFact]
        public void It_does_not_include_shims_when_app_references_471_library_and_461_library()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_Net471Library",
                TargetFrameworks = "net471",
                IsExe = true
            };

            var net471library = new TestProject()
            {
                Name = "Net471_Library",
                TargetFrameworks = "net471",
            };

            var net462library = new TestProject()
            {
                Name = "net462_Library",
                TargetFrameworks = "net462",
            };

            testProject.ReferencedProjects.Add(net471library);
            testProject.ReferencedProjects.Add(net462library);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_net471_net462");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.exe.config",
                $"{testProject.Name}.pdb",
                $"{net471library.Name}.dll",
                $"{net471library.Name}.pdb",
                $"{net462library.Name}.dll",
                $"{net462library.Name}.pdb",
            });
        }

        [WindowsOnlyFact]
        public void It_contains_shims_if_override_property_is_set()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App",
                TargetFrameworks = "net471",
                IsExe = true
            };

            testProject.AdditionalProperties.Add("DependsOnNETStandard", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_with_override_property");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.exe.config", // We have now added binding redirects so we should expect a config flag to be dropped to the output directory.
            }.Concat(net471Shims));
        }

        
        [WindowsOnlyFact]
        public void Aliases_are_preserved_for_replaced_references()
        {
            var testProject = new TestProject()
            {
                Name = "Net471AliasTest",
                TargetFrameworks = "net471",
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name = "NetStandard20_Library",
                TargetFrameworks = "netstandard2.0",
            };

            testProject.SourceFiles["Program.cs"] = $@"
extern alias snh;
using System;
public static class Program
{{
    public static void Main()
    {{
        new snh::System.Net.Http.HttpClient();
        Console.WriteLine(""Hello, World!"");
        Console.WriteLine({netStandardProject.Name}.{netStandardProject.Name}Class.Name);
    }}
}}
";

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetFileNameWithoutExtension(projectPath) == testProject.Name)
                    {
                        var ns = project.Root.Name.Namespace;

                        var referenceElement = new XElement(ns + "Reference",
                                                            new XAttribute("Include", "System.Net.Http"),
                                                            new XAttribute("Aliases", "snh"));

                        project.Root.Add(new XElement(ns + "ItemGroup", referenceElement));
                    }
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [FullMSBuildOnlyFact]
        public void ZipFileCanBeSharedWithNetStandard16()
        {
            TestZipFileSharing(false);
        }


        [WindowsOnlyFact]
        public void ZipFileCanBeSharedWithNetStandard16_sdk()
        {
            TestZipFileSharing(true);
        }
        private void TestZipFileSharing(bool useSdk, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject()
            {
                Name = "Net471ZipFileTest",
                IsExe = true,
                IsSdkProject = useSdk

            };

            if (useSdk)
            {
                testProject.TargetFrameworks = "net471";
            }
            else
            {
                testProject.TargetFrameworkVersion = "v4.7.1";
            }

            testProject.PackageReferences.AddRange(new[]
            {
                new TestPackageReference("System.IO.Compression.ZipFile", "4.3.0")
            });


            var netStandardProject = new TestProject()
            {
                Name = "NetStandard16_Library",
                TargetFrameworks = "netstandard1.6",
            };

            netStandardProject.PackageReferences.AddRange(new[]
            {
                new TestPackageReference("System.IO.Compression.ZipFile", "4.3.0")
            });

            testProject.ReferencedProjects.Add(netStandardProject);

            testProject.SourceFiles["Program.cs"] = $@"
using System;
public static class Program
{{
    public static int Main()
    {{
        bool success = true;

        Type[] nsTypes = NS16LibClass.GetTypes();
        Type[] appTypes = new Type[]
        {{
            typeof(System.IO.Compression.ZipArchive),
            typeof(System.IO.Compression.ZipArchiveEntry),
            typeof(System.IO.Compression.ZipArchiveMode),
            typeof(System.IO.Compression.ZipFile),
            typeof(System.IO.Compression.ZipFileExtensions),
        }};

        if (nsTypes.Length != appTypes.Length)
        {{
            Console.WriteLine($""Error: Types count in NS library {{ nsTypes.Length}} is not equal to the types count in the app {{ appTypes.Length}} "");
            return 1;
        }}

        for (int i = 0; i < nsTypes.Length; i++)
        {{
            if (!nsTypes[i].Equals(appTypes[i]))
            {{
                Console.WriteLine($""{{nsTypes[i].FullName}}"");
                success = false;
            }}
        }}

        if (success)
        {{
            Console.WriteLine(""Success"");
            return 0;
        }}
        return 1;
    }}
}}
";

            netStandardProject.SourceFiles["NSTypes.cs"] = $@"
using System;
public static class NS16LibClass
{{
    public static Type [] GetTypes()
    {{
        return new Type[]
        {{
            typeof(System.IO.Compression.ZipArchive),
            typeof(System.IO.Compression.ZipArchiveEntry),
            typeof(System.IO.Compression.ZipArchiveMode),
            typeof(System.IO.Compression.ZipFile),
            typeof(System.IO.Compression.ZipFileExtensions),
        }};
    }}
}}
";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod: callingMethod, identifier: useSdk ? "_sdk" : string.Empty)
                            .WithProjectChanges((projectPath, project) =>
                            {
                                if (Path.GetFileNameWithoutExtension(projectPath) == testProject.Name)
                                {
                                    string folder = Path.GetDirectoryName(projectPath);

                                    //  Adding this binding redirect is the workaround for ZipFile on .NET 4.7.1
                                    //  See https://github.com/Microsoft/dotnet/blob/master/releases/net471/KnownIssues/623552-BCL%20Higher%20assembly%20versions%20that%204.0.0.0%20for%20System.IO.Compression.ZipFile%20cannot%20be%20loaded%20without%20a%20binding%20redirect.md
                                    File.WriteAllText(Path.Combine(folder, "app.config"),
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <runtime>
    <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
      <dependentAssembly>
        <assemblyIdentity name=""System.IO.Compression.ZipFile"" publicKeyToken=""b77a5c561934e089"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-4.0.2.0"" newVersion=""4.0.0.0"" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
");
                                    var ns = project.Root.Name.Namespace;

                                    project.Root.Elements(ns + "ItemGroup").Last().Add(
                                        new XElement(ns + "None", new XAttribute("Include", "app.config")));

                                }
                            });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:m")
                .Should()
                .Pass()
                .And
                //  warning MSB3836: The explicit binding redirect on "System.IO.Compression.ZipFile, Culture=neutral, PublicKeyToken=b77a5c561934e089"
                //  conflicts with an autogenerated binding redirect. Consider removing it from the application configuration file or disabling
                //  autogenerated binding redirects. The build will replace it with: "<bindingRedirect oldVersion="0.0.0.0-4.0.3.0" newVersion="4.0.3.0"
                //  xmlns="urn:schemas-microsoft-com:asm.v1" />"
                .NotHaveStdOutContaining("MSB3836");

            var exePath = Path.Combine(buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName, testProject.Name + ".exe");

            new RunExeCommand(Log, exePath)
                .Execute()
                .Should()
                .Pass();


        }

        //  Regression test for https://github.com/dotnet/sdk/issues/2479
        [FullMSBuildOnlyFact]
        public void HttpClient_can_be_used_in_project_references()
        {
            var referencedProject = new TestProject()
            {
                Name = "ReferencedHttpClientProject",
                IsExe = false,
                IsSdkProject = false,
                TargetFrameworkVersion = "v4.7.1"
            };
            referencedProject.PackageReferences.Add(new TestPackageReference("dotless.Core", "1.6.4"));
            referencedProject.PackageReferences.Add(new TestPackageReference("Microsoft.Owin.Security.Facebook", "4.0.0"));

            referencedProject.SourceFiles["FacebookHandler.cs"] = @"
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class FacebookHandler : HttpClientHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
        return await base.SendAsync(request, cancellationToken);
    }
}";

            var testProject = new TestProject()
            {
                Name = "Net471HttpClientTest",
                IsExe = true,
                IsSdkProject = false,
                TargetFrameworkVersion = "v4.7.1"
            };

            testProject.AdditionalProperties["RestoreProjectStyle"] = "PackageReference";

            testProject.ReferencedProjects.Add(referencedProject);
            testProject.SourceFiles["Program.cs"] = @"
using Microsoft.Owin.Security.Facebook;
using Owin;

public class Startup
{
    public static void Main(string [] args)
    {
    }

    public void Configuration(IAppBuilder app)
    {
        var facebookOptions = new FacebookAuthenticationOptions
        {
            BackchannelHttpHandler = new FacebookHandler()
        };
    }
}
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetFileNameWithoutExtension(projectPath) == testProject.Name)
                    {
                        var ns = project.Root.Name.Namespace;

                        //  Add target which helped trigger the error case.  A target like this was provided as
                        //  a workaround to a different issue: https://github.com/dotnet/sdk/pull/1582#issuecomment-329571228
                        var reproTarget = XElement.Parse(@"
  <Target Name=""AddAdditionalReference"" AfterTargets=""ImplicitlyExpandNETStandardFacades"">
    <ItemGroup>
      <Reference Include = ""@(_NETStandardLibraryNETFrameworkLib)"" Condition = ""'%(FileName)' == 'system.net.http'"">
        <Private>true</Private>
      </Reference>
    </ItemGroup>
  </Target> ");
                        foreach (var element in reproTarget.DescendantsAndSelf())
                        {
                            element.Name = ns + element.Name.LocalName;
                        }
                        project.Root.Add(reproTarget);
                    }
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute().Should().Pass();
        }
    }
}
