using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopLibrary : SdkTest
    {
        [Fact]
        public void It_can_use_HttpClient_and_exchange_the_type_with_a_NETStandard_library()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var netStandardLibrary = new TestProject()
            {
                Name = "NETStandardLibrary",
                TargetFrameworks = "netstandard1.4",
                IsSdkProject = true
            };

            netStandardLibrary.SourceFiles["NETStandard.cs"] = @"
using System.Net.Http;
public class NETStandard
{
    public static HttpClient GetHttpClient()
    {
        return new HttpClient();
    }
}
";

            var netFrameworkLibrary = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net461",
                IsSdkProject = true
            };

            netFrameworkLibrary.ReferencedProjects.Add(netStandardLibrary);

            netFrameworkLibrary.SourceFiles["NETFramework.cs"] = @"
using System.Net.Http;
public class NETFramework
{
    public void Method1()
    {
        System.Net.Http.HttpClient client = NETStandard.GetHttpClient();
    }
}
";

            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "ExchangeHttpClient")
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetFileName(projectPath).Equals(netFrameworkLibrary.Name + ".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var ns = project.Root.Name.Namespace;

                        var itemGroup = new XElement(ns + "ItemGroup");
                        project.Root.Add(itemGroup);

                        itemGroup.Add(new XElement(ns + "Reference", new XAttribute("Include", "System.Net.Http")));
                    }
                })
                .Restore(netFrameworkLibrary.Name);

            var buildCommand = new BuildCommand(MSBuildTest.Stage0MSBuild, Path.Combine(testAsset.TestRoot, netFrameworkLibrary.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }
    }
}
