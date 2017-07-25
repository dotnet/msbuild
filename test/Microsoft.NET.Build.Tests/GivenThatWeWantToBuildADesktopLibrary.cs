using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopLibrary : SdkTest
    {
        public GivenThatWeWantToBuildADesktopLibrary(ITestOutputHelper log) : base(log)
        {
        }

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
                .Restore(Log, netFrameworkLibrary.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, netFrameworkLibrary.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }

        [WindowsOnlyFact]
        public void It_can_reference_a_netstandard2_library_and_exchange_types()
        {

            var netStandardLibrary = new TestProject()
            {
                Name = "NETStandardLibrary",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            netStandardLibrary.SourceFiles["NETStandard.cs"] = @"
public class NETStandard
{
    public static string GetString()
    {
        return ""Hello from netstandard2.0 library."";
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
public class NETFramework
{
    public void Method1()
    {
        string result = NETStandard.GetString();
    }
}
";
            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "ExchangeNETStandard2")
                .Restore(Log, netFrameworkLibrary.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, netFrameworkLibrary.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_can_use_ValueTuple_and_exchange_the_type_with_a_NETStandard_library()
        {
            var referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version47);
            if (!Directory.Exists(referenceAssemblies))
            {
                return;
            }

            var netStandardLibrary = new TestProject()
            {
                Name = "NETStandardLibrary",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            netStandardLibrary.SourceFiles["NETStandard.cs"] = @"
public class NETStandard
{
    public static (int x, int y) GetCoordinates()
    {
        return (1, 10);
    }
}
";
            // ValueTuple was moved into mscorlib in net47, make sure we include net47-specific build of netstandard libs
            // that typeforward to mscorlib.
            var netFrameworkLibrary = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net47",
                IsSdkProject = true
            };
            netFrameworkLibrary.ReferencedProjects.Add(netStandardLibrary);

            netFrameworkLibrary.SourceFiles["NETFramework.cs"] = @"
using System;

public class NETFramework
{
    public void Method1()
    {
        var coords = NETStandard.GetCoordinates();
        Console.WriteLine($""({coords.x}, {coords.y})"");
        ValueTuple<int,int> vt = coords;
    }
}
";
            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "ExchangeValueTuple")
                .Restore(Log, netFrameworkLibrary.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, netFrameworkLibrary.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_can_preserve_compilation_context_and_reference_netstandard_library()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopReferencingNetStandardLibrary")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should().Pass();

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "Library.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext.CompileLibraries.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void It_resolves_assembly_conflicts_with_a_NETFramework_library()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            TestProject project = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
                IsSdkProject = true
            };

            project.SourceFiles[project.Name + ".cs"] = $@"
using System;
public static class {project.Name}
{{
    {ConflictResolutionAssets.ConflictResolutionTestMethod}
}}";

            var testAsset = _testAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                        new XAttribute("Include", "NETStandard.Library"),
                        new XAttribute("Version", "$(BundledNETStandardPackageVersion)")));

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                })
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning")
                .And
                .NotHaveStdOutContaining("MSB3243");
        }

        [Fact]
        public void It_uses_hintpath_when_replacing_simple_name_references()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            TestProject project = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
                IsSdkProject = true
            };


            var testAsset = _testAssetsManager.CreateTestProject(project)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                        new XAttribute("Include", "System.Net.Http"),
                        new XAttribute("Version", "4.3.2")));

                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", "System.Net.Http")));
                })
                .Restore(Log, project.Name);

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var getValuesCommand = new GetValuesCommand(Log, projectFolder, project.TargetFrameworks, "Reference", GetValuesCommand.ValueType.Item);
            getValuesCommand.MetadataNames.Add("HintPath");

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            string nugetReferencePath = Path.Combine(RepoInfo.NuGetCachePath, "system.net.http", "4.3.2", "ref", "net46", "System.Net.Http.dll");

            var valuesWithMetadata = getValuesCommand.GetValuesWithMetadata();

            //  There shouldn't be a Reference item where the ItemSpec is the path to the System.Net.Http.dll from a NuGet package
            valuesWithMetadata.Should().NotContain(v => v.value == nugetReferencePath);

            //  There should be a Reference item where the ItemSpec is the simple name System.Net.Http
            valuesWithMetadata.Should().ContainSingle(v => v.value == "System.Net.Http");
            
            //  The Reference item with the simple name should have a HintPath to the DLL in the NuGet package
            valuesWithMetadata.Single(v => v.value == "System.Net.Http")
                .metadata["HintPath"]
                .Should().Be(nugetReferencePath);            
        }
    }
}
