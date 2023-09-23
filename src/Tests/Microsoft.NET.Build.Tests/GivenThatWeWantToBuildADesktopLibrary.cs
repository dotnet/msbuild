// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopLibrary : SdkTest
    {
        public GivenThatWeWantToBuildADesktopLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_gets_implicit_designtime_facades_when_package_reference_uses_system_runtime()
        {
            // The repro here is very sensitive to the target framework and packages used. This specific case
            // net46 using only System.Collections.Immutable v1.4.0 will not pull in System.Runtime from a
            // package or from Microsoft.NET.Build.Extensions as a primary reference and so RARs dependency
            // walk needs to find it in order for ImplicitlyExpandDesignTimeFacades to inject it.

            var netFrameworkLibrary = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net46",
            };

            netFrameworkLibrary.PackageReferences.Add(new TestPackageReference("System.Collections.Immutable", "1.4.0"));

            netFrameworkLibrary.SourceFiles["NETFramework.cs"] = @"
                using System.Collections.Immutable;
                public class NETFramework
                {
                    public void Method1()
                    {
                        ImmutableList<string>.Empty.Add("""");
                    }
                }";

            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "FacadesFromTargetFramework");
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();
        }

        [WindowsOnlyFact]
        public void It_can_use_HttpClient_and_exchange_the_type_with_a_NETStandard_library()
        {
            var netStandardLibrary = new TestProject()
            {
                Name = "NETStandardLibrary",
                TargetFrameworks = "netstandard1.4",
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
                TargetFrameworks = "net462",
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
                });

            var buildCommand = new BuildCommand(testAsset);

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
                TargetFrameworks = "net462",
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
            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "ExchangeNETStandard2");

            var buildCommand = new BuildCommand(testAsset);

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
            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "ExchangeValueTuple");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_can_preserve_compilation_context_and_reference_netstandard_library()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopReferencingNetStandardLibrary")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should().Pass();

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "Library.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext.CompileLibraries.Should().NotBeEmpty();
            }
        }

        [WindowsOnlyFact]
        public void It_resolves_assembly_conflicts_with_a_NETFramework_library()
        {
            TestProject project = new()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
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
                        new XAttribute("Version", "2.0.3")));

                    foreach (var dependency in ConflictResolutionAssets.ConflictResolutionDependencies)
                    {
                        itemGroup.Add(new XElement(ns + "PackageReference",
                            new XAttribute("Include", dependency.Item1),
                            new XAttribute("Version", dependency.Item2)));
                    }

                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("warning")
                .And
                .NotHaveStdOutContaining("MSB3243");
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_uses_hintpath_when_replacing_simple_name_references(bool useFacades)
        {
            TestProject project = new()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
            };

            if (useFacades)
            {
                var netStandard2Project = new TestProject()
                {
                    Name = "NETStandard20Project",
                    TargetFrameworks = "netstandard2.0",
                };

                project.ReferencedProjects.Add(netStandard2Project);
            }


            var testAsset = _testAssetsManager.CreateTestProject(project, "SimpleNamesWithHintPaths", identifier: useFacades ? "_useFacades" : "")
                .WithProjectChanges((path, p) =>
                {
                    if (Path.GetFileNameWithoutExtension(path) == project.Name)
                    {
                        var ns = p.Root.Name.Namespace;

                        var itemGroup = new XElement(ns + "ItemGroup");
                        p.Root.Add(itemGroup);

                        if (!useFacades)
                        {
                            itemGroup.Add(new XElement(ns + "PackageReference",
                                new XAttribute("Include", "System.Net.Http"),
                                new XAttribute("Version", "4.3.2")));
                        }

                        itemGroup.Add(new XElement(ns + "Reference",
                            new XAttribute("Include", "System.Net.Http")));
                    }
                });

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var getValuesCommand = new GetValuesCommand(Log, projectFolder, project.TargetFrameworks, "Reference", GetValuesCommand.ValueType.Item);
            getValuesCommand.MetadataNames.Add("HintPath");

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            string correctHttpReference;
            if (useFacades)
            {
                string microsoftNETBuildExtensionsPath = TestContext.Current.ToolsetUnderTest.GetMicrosoftNETBuildExtensionsPath();
                correctHttpReference = Path.Combine(microsoftNETBuildExtensionsPath, @"net461\lib\System.Net.Http.dll");
            }
            else
            {
                correctHttpReference = Path.Combine(TestContext.Current.NuGetCachePath, "system.net.http", "4.3.2", "ref", "net46", "System.Net.Http.dll");
            }

            var valuesWithMetadata = getValuesCommand.GetValuesWithMetadata();

            //  There shouldn't be a Reference item where the ItemSpec is the path to the System.Net.Http.dll from a NuGet package
            valuesWithMetadata.Should().NotContain(v => v.value == correctHttpReference);

            //  There should be a Reference item where the ItemSpec is the simple name System.Net.Http
            valuesWithMetadata.Should().ContainSingle(v => v.value == "System.Net.Http");

            //  The Reference item with the simple name should have a HintPath to the DLL in the NuGet package
            valuesWithMetadata.Single(v => v.value == "System.Net.Http")
                .metadata["HintPath"]
                .Should().Be(correctHttpReference);
        }

        [Fact]
        public void It_tolerates_newline_in_hint_path()
        {
            string hintPath = BuildReferencedBuildAndReturnOutputDllPath();

            TestProject project = new()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
            };

            TestAsset testAsset = _testAssetsManager.CreateTestProject(project, "SimpleNamesWithHintPathsWithNewLines")
                .WithProjectChanges((path, p) =>
                {
                    XNamespace ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(
                        new XElement(ns + "Reference",
                            new XAttribute("Include", "System.Net.Http"),
                            new XElement("HintPath", $"   {Environment.NewLine}{hintPath}   {Environment.NewLine}")));
                });

            var buildCommand = new BuildCommand(testAsset);
            var msbuildBuildCommand = new MSBuildCommand(Log, "Build", buildCommand.FullPathProjectFile);
            msbuildBuildCommand.Execute().Should().Pass()
                .And.NotHaveStdOutContaining("System.ArgumentException");
        }

        private string BuildReferencedBuildAndReturnOutputDllPath()
        {
            TestProject referencedProject = new()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
            };

            TestAsset referencedTestAsset = _testAssetsManager
                .CreateTestProject(referencedProject, "SimpleNamesWithHintPathsWithNewLinesReferenced");

            var referencedbuildCommand =
                new BuildCommand(referencedTestAsset);

            referencedbuildCommand.Execute();

            DirectoryInfo outputDirectory = referencedbuildCommand.GetOutputDirectory(
                referencedProject.TargetFrameworks);
            return new FileInfo(Path.Combine(outputDirectory.FullName, referencedProject.Name + ".dll")).FullName;
        }

        //  Regression test for https://github.com/dotnet/sdk/issues/1730
        [WindowsOnlyFact]
        public void A_target_can_depend_on_RunResolvePublishAssemblies()
        {
            TestProject testProject = new()
            {
                Name = "DependsOnPublish",
                TargetFrameworks = "net462",
                IsExe = false
            };

            var testInstance = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var pns = p.Root.Name.Namespace;

                    p.Root.Add(new XElement(pns + "Target",
                        new XAttribute("Name", "Repro"),
                        new XAttribute("DependsOnTargets", "RunResolvePublishAssemblies"),
                        new XAttribute("BeforeTargets", "BeforeBuild")));
                });

            var buildCommand = new BuildCommand(testInstance);

            buildCommand.Execute()
                .Should()
                .Pass();
        }
    }
}
