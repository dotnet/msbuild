// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using FluentAssertions;

using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit;
using Xunit.Abstractions;

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
            // walk needs to find it in order for ImplictlyExpandDesignTimeFacades to inject it.

            var netFrameworkLibrary = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net46",
                IsSdkProject = true,
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

            var testAsset = _testAssetsManager.CreateTestProject(netFrameworkLibrary, "FacadesFromTargetFramework").Restore(Log, netFrameworkLibrary.Name);
            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, netFrameworkLibrary.Name));
            buildCommand.Execute().Should().Pass();
        }

        [WindowsOnlyFact]
        public void It_can_use_HttpClient_and_exchange_the_type_with_a_NETStandard_library()
        {
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

        [WindowsOnlyFact]
        public void It_can_preserve_compilation_context_and_reference_netstandard_library()
        {
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

        [WindowsOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/1803")]
        public void It_resolves_assembly_conflicts_with_a_NETFramework_library()
        {
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
                .Restore(Log, project.Name, "/p:RestoreSources=https://dotnetfeed.blob.core.windows.net/dotnet-core/packages/index.json;https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json;https://dotnet.myget.org/F/dotnet-core/api/v3/index.json;https://dotnet.myget.org/F/msbuild/api/v3/index.json;https://dotnet.myget.org/F/nuget-build/api/v3/index.json");

            string projectFolder = Path.Combine(testAsset.Path, project.Name);

            var buildCommand = new BuildCommand(Log, projectFolder);

            buildCommand
                .Execute("/p:RestoreSources=https://dotnetfeed.blob.core.windows.net/dotnet-core/packages/index.json;https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json;https://dotnet.myget.org/F/dotnet-core/api/v3/index.json;https://dotnet.myget.org/F/msbuild/api/v3/index.json;https://dotnet.myget.org/F/nuget-build/api/v3/index.json")
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
            TestProject project = new TestProject()
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
                IsSdkProject = true
            };

            if (useFacades)
            {
                var netStandard2Project = new TestProject()
                {
                    Name = "NETStandard20Project",
                    TargetFrameworks = "netstandard2.0",
                    IsSdkProject = true
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
                })
                .Restore(Log, project.Name);

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
                correctHttpReference = Path.Combine(TestContext.Current.ToolsetUnderTest.BuildExtensionsMSBuildPath, @"net461\lib\System.Net.Http.dll");
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

        [WindowsOnlyTheory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void It_marks_extension_references_as_externally_resolved(bool? markAsExternallyResolved)
        {
            var project = new TestProject
            {
                Name = "NETFrameworkLibrary",
                TargetFrameworks = "net462",
                IsSdkProject = true
            };

            var netStandard2Project = new TestProject
            {
                Name = "NETStandard20Project",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            project.ReferencedProjects.Add(netStandard2Project);

            var asset = _testAssetsManager.CreateTestProject(
                project,
                "ExternallyResolvedExtensions",
                markAsExternallyResolved.ToString())
                .WithProjectChanges((path, p) =>
                {
                    if (markAsExternallyResolved != null)
                    {
                        var ns = p.Root.Name.Namespace;
                        p.Root.Add(
                            new XElement(ns + "PropertyGroup",
                                new XElement(ns + "MarkNETFrameworkExtensionAssembliesAsExternallyResolved",
                                    markAsExternallyResolved)));
                    }
                })
                .Restore(Log, project.Name);

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, project.Name),
                project.TargetFrameworks,
                "Reference",
                GetValuesCommand.ValueType.Item);

            command.MetadataNames.AddRange(new[] { "ExternallyResolved", "HintPath" });
            command.Execute().Should().Pass();

            int frameworkReferenceCount = 0;
            int extensionReferenceCount = 0;
            var references = command.GetValuesWithMetadata();

            foreach (var (value, metadata) in references)
            {
                if (metadata["HintPath"] == "")
                {
                    // implicit framework reference (not externally resolved)
                    metadata["ExternallyResolved"].Should().BeEmpty();
                    frameworkReferenceCount++;
                }
                else
                {
                    // reference added by Microsoft.NET.Build.Extensions
                    metadata["ExternallyResolved"].Should().BeEquivalentTo((markAsExternallyResolved ?? true).ToString());
                    extensionReferenceCount++;
                }
            }

            // make sure both cases were encountered
            frameworkReferenceCount.Should().BeGreaterThan(0);
            extensionReferenceCount.Should().BeGreaterThan(0);
        }
    }
}
