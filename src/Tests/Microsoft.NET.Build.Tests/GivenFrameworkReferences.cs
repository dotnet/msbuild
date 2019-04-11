using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenFrameworkReferences : SdkTest
    {
        public GivenFrameworkReferences(ITestOutputHelper log) : base(log)
        {
        }

        //  Tests in this class are currently Core MSBuild only, as they check for PackageDownload items,
        //  which are currently only used in Core MSBuild
        [CoreMSBuildAndWindowsOnlyFact]
        public void Multiple_frameworks_are_written_to_runtimeconfig_when_there_are_multiple_FrameworkReferences()
        {
            var testProject = new TestProject()
            {
                Name = "MultipleFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.SourceFiles.Add("Program.cs", @"
using System;

namespace FrameworkReferenceTest
{
    public class Program
    {
        public static void Main(string [] args)
        {
        }
    }
}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.AspNetCore.App")));
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.WindowsDesktop.App")));


                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

            var runtimeFrameworksList = (JArray)runtimeConfig["runtimeOptions"]["frameworks"];
            var runtimeFrameworkNames = runtimeFrameworksList.Select(element => ((JValue)element["name"]).Value<string>());

            //  When we remove the workaround for https://github.com/dotnet/core-setup/issues/4947 in GenerateRuntimeConfigurationFiles,
            //  Microsoft.NETCore.App will need to be added to this list
            runtimeFrameworkNames.Should().BeEquivalentTo("Microsoft.AspNetCore.App", "Microsoft.WindowsDesktop.App");
        }

        [CoreMSBuildOnlyFact]
        public void The_build_fails_when_there_is_an_unknown_FrameworkReference()
        {
            var testProject = new TestProject()
            {
                Name = "UnknownFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "NotAKnownFramework")));
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "AnotherUnknownFramework")));

                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1073")
                .And.HaveStdOutContaining("NotAKnownFramework")
                .And.HaveStdOutContaining("AnotherUnknownFramework")
                ;
        }

        [CoreMSBuildOnlyTheory]
        [InlineData("netcoreapp2.1", false)]
        [InlineData("netcoreapp3.0", true)]
        public void KnownFrameworkReferencesOnlyApplyToCorrectTargetFramework(string targetFramework, bool shouldPass)
        {
            var testProject = new TestProject()
            {
                Name = "FrameworkReferenceTest",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.AspNetCore.App")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var result = buildCommand.Execute();

            if (shouldPass)
            {
                result.Should().Pass();
            }
            else
            {
                result
                    .Should()
                    .Fail()
                    .And.HaveStdOutContaining("NETSDK1073")
                    .And.HaveStdOutContaining("Microsoft.AspNetCore.App");
            }
        }
        [CoreMSBuildOnlyFact]
        public void TargetingPackDownloadCanBeDisabled()
        {
            var testProject = new TestProject()
            {
                Name = "DisableTargetingPackDownload",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["EnableTargetingPackDownload"] = "False";

            //  Set targeting pack folder to nonexistant folder so the project won't use installed targeting packs
            testProject.AdditionalProperties["NetCoreTargetingPackRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            string nugetPackagesFolder = Path.Combine(testAsset.TestRoot, "packages");


            var restoreCommand = new RestoreCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .WithEnvironmentVariable("NUGET_PACKAGES", nugetPackagesFolder);
            restoreCommand.Execute().Should().Pass();


            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .WithEnvironmentVariable("NUGET_PACKAGES", nugetPackagesFolder);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1073");
        }

        [CoreMSBuildOnlyFact]
        public void BuildFailsIfRuntimePackIsNotAvailableForRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "RuntimePackNotAvailable",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true,
                RuntimeIdentifier = "linux-x64"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    var frameworkReference = new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.WindowsDesktop.App"));
                    itemGroup.Add(frameworkReference);
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
                .Execute("/clp:summary")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1082")
                .And
                .HaveStdOutContaining("1 Error(s)");
        }

        [CoreMSBuildOnlyFact]
        public void BuildFailsIfInvalidRuntimeIdentifierIsSpecified()
        {
            var testProject = new TestProject()
            {
                Name = "RuntimePackNotAvailable",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true,
                RuntimeIdentifier = "invalid-rid"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            restoreCommand
                //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
                .Execute("/clp:summary")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1083")
                .And
                .HaveStdOutContaining("1 Error(s)");
        }

        [CoreMSBuildOnlyFact]
        public void BuildFailsIfRuntimePackHasNotBeenRestored()
        {
            var testProject = new TestProject()
            {
                Name = "RuntimePackNotRestored",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true,
            };
            
            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";

            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot, testProject.Name);

            //  If we do the work in https://github.com/dotnet/cli/issues/10528,
            //  then we should add a new error message here indicating that the runtime pack hasn't
            //  been downloaded.
            string expectedErrorCode = "NETSDK1047";

            buildCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(expectedErrorCode);

        }

        [CoreMSBuildOnlyFact]
        public void RuntimeFrameworkVersionCanBeSpecifiedOnFrameworkReference()
        {
            var testProject = new TestProject();

            string runtimeFrameworkVersion = "3.0.0-runtimeframeworkversion-attribute";
            string targetingPackVersion = "3.0.0-targetingpackversion";

            testProject.AdditionalProperties["RuntimeFrameworkVersion"] = "3.0.0-runtimeframeworkversion-property";

            var resolvedVersions = GetResolvedVersions(testProject,
                project =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Elements(ns + "ItemGroup")
                        .Elements(ns + "FrameworkReference")
                        .Single(fr => fr.Attribute("Include").Value == "Microsoft.NETCore.App")
                        .SetAttributeValue("RuntimeFrameworkVersion", runtimeFrameworkVersion);
                });

            resolvedVersions.RuntimeFramework["Microsoft.NETCore.App"].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.PackageDownload["Microsoft.NETCore.App.Ref"].Should().Be(targetingPackVersion);
            string runtimePackName = resolvedVersions.PackageDownload.Keys
                .Where(k => k.StartsWith("runtime.") && k.EndsWith(".Microsoft.NETCore.App"))
                .Single();
            resolvedVersions.PackageDownload[runtimePackName].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.TargetingPack["Microsoft.NETCore.App"].Should().Be(targetingPackVersion);
            resolvedVersions.RuntimePack[runtimePackName].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.AppHostPack["AppHost"].Should().Be("3.0.0-runtimeframeworkversion-property");
        }

        [CoreMSBuildOnlyFact]
        public void RuntimeFrameworkVersionCanBeSpecifiedViaProperty()
        {
            var testProject = new TestProject();

            string runtimeFrameworkVersion = "3.0.0-runtimeframeworkversion-property";
            string targetingPackVersion = "3.0.0-targetingpackversion";

            testProject.AdditionalProperties["RuntimeFrameworkVersion"] = runtimeFrameworkVersion;

            var resolvedVersions = GetResolvedVersions(testProject);

            resolvedVersions.RuntimeFramework["Microsoft.NETCore.App"].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.PackageDownload["Microsoft.NETCore.App.Ref"].Should().Be(targetingPackVersion);
            string runtimePackName = resolvedVersions.PackageDownload.Keys
                .Where(k => k.StartsWith("runtime.") && k.EndsWith(".Microsoft.NETCore.App"))
                .Single();
            resolvedVersions.PackageDownload[runtimePackName].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.TargetingPack["Microsoft.NETCore.App"].Should().Be(targetingPackVersion);
            resolvedVersions.RuntimePack[runtimePackName].Should().Be(runtimeFrameworkVersion);
            resolvedVersions.AppHostPack["AppHost"].Should().Be(runtimeFrameworkVersion);
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void TargetLatestPatchCanBeSpecifiedOnFrameworkReference(bool attributeValue)
        {
            var testProject = new TestProject();

            string targetingPackVersion = "3.0.0-targetingpackversion";

            testProject.AdditionalProperties["TargetLatestRuntimePatch"] = (!attributeValue).ToString();

            var resolvedVersions = GetResolvedVersions(testProject,
                project =>
                {
                    var ns = project.Root.Name.Namespace;

                project.Root.Elements(ns + "ItemGroup")
                    .Elements(ns + "FrameworkReference")
                    .Single(fr => fr.Attribute("Include").Value == "Microsoft.NETCore.App")
                    .SetAttributeValue("TargetLatestRuntimePatch", attributeValue.ToString());
                },
                identifier: attributeValue.ToString());

            string expectedRuntimeFrameworkVersion = attributeValue ? "3.0.0-latestversion" : "3.0.0-defaultversion";

            resolvedVersions.RuntimeFramework["Microsoft.NETCore.App"].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.PackageDownload["Microsoft.NETCore.App.Ref"].Should().Be(targetingPackVersion);
            string runtimePackName = resolvedVersions.PackageDownload.Keys
                .Where(k => k.StartsWith("runtime.") && k.EndsWith(".Microsoft.NETCore.App"))
                .Single();
            resolvedVersions.PackageDownload[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.TargetingPack["Microsoft.NETCore.App"].Should().Be(targetingPackVersion);
            resolvedVersions.RuntimePack[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.AppHostPack["AppHost"].Should().Be("3.0.0-apphostversion");
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void TargetLatestPatchCanBeSpecifiedViaProperty(bool propertyValue)
        {
            var testProject = new TestProject();

            string targetingPackVersion = "3.0.0-targetingpackversion";

            testProject.AdditionalProperties["TargetLatestRuntimePatch"] = propertyValue.ToString();

            var resolvedVersions = GetResolvedVersions(testProject, identifier: propertyValue.ToString());

            string expectedRuntimeFrameworkVersion = propertyValue ? "3.0.0-latestversion" : "3.0.0-defaultversion";

            resolvedVersions.RuntimeFramework["Microsoft.NETCore.App"].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.PackageDownload["Microsoft.NETCore.App.Ref"].Should().Be(targetingPackVersion);
            string runtimePackName = resolvedVersions.PackageDownload.Keys
                .Where(k => k.StartsWith("runtime.") && k.EndsWith(".Microsoft.NETCore.App"))
                .Single();
            resolvedVersions.PackageDownload[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.TargetingPack["Microsoft.NETCore.App"].Should().Be(targetingPackVersion);
            resolvedVersions.RuntimePack[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.AppHostPack["AppHost"].Should().Be("3.0.0-apphostversion");
        }

        [CoreMSBuildOnlyFact]
        public void TargetingPackVersionCanBeSpecifiedOnFrameworkReference()
        {
            var testProject = new TestProject();

            string targetingPackVersion = "3.0.0-tpversionfromframeworkreference";

            var resolvedVersions = GetResolvedVersions(testProject,
                project =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Elements(ns + "ItemGroup")
                        .Elements(ns + "FrameworkReference")
                        .Single(fr => fr.Attribute("Include").Value == "Microsoft.NETCore.App")
                        .SetAttributeValue("TargetingPackVersion", targetingPackVersion);
                });

            string expectedRuntimeFrameworkVersion = "3.0.0-latestversion";

            resolvedVersions.RuntimeFramework["Microsoft.NETCore.App"].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.PackageDownload["Microsoft.NETCore.App.Ref"].Should().Be(targetingPackVersion);
            string runtimePackName = resolvedVersions.PackageDownload.Keys
                .Where(k => k.StartsWith("runtime.") && k.EndsWith(".Microsoft.NETCore.App"))
                .Single();
            resolvedVersions.PackageDownload[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.TargetingPack["Microsoft.NETCore.App"].Should().Be(targetingPackVersion);
            resolvedVersions.RuntimePack[runtimePackName].Should().Be(expectedRuntimeFrameworkVersion);
            resolvedVersions.AppHostPack["AppHost"].Should().Be("3.0.0-apphostversion");
        }

        private ResolvedVersionInfo GetResolvedVersions(TestProject testProject,
            Action<XDocument> projectChanges = null,
            [CallerMemberName] string callingMethod = null,
            string identifier = null)
        {
            testProject.Name = "ResolvedVersionsTest";
            testProject.TargetFrameworks = "netcoreapp3.0";
            testProject.IsSdkProject = true;
            testProject.IsExe = true;
            testProject.AdditionalProperties["DisableImplicitFrameworkReferences"] = "true";
            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    var frameworkReference = new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.NETCore.App"));
                    itemGroup.Add(frameworkReference);

                    var knownFrameworkReferenceUpdate = new XElement(ns + "KnownFrameworkReference",
                                                                     new XAttribute("Update", "Microsoft.NETCore.App"),
                                                                     new XAttribute("DefaultRuntimeFrameworkVersion", "3.0.0-defaultversion"),
                                                                     new XAttribute("LatestRuntimeFrameworkVersion", "3.0.0-latestversion"),
                                                                     new XAttribute("TargetingPackVersion", "3.0.0-targetingpackversion"));
                    itemGroup.Add(knownFrameworkReferenceUpdate);

                    var knownAppHostPackUpdate = new XElement(ns + "KnownAppHostPack",
                                                            new XAttribute("Update", "Microsoft.NETCore.App"),
                                                            new XAttribute("AppHostPackVersion", "3.0.0-apphostversion"));

                    itemGroup.Add(knownAppHostPackUpdate);

                    string writeResolvedVersionsTarget = @"
<Target Name=`WriteResolvedVersions` DependsOnTargets=`PrepareForBuild;ResolveFrameworkReferences`>
    <ItemGroup>
      <LinesToWrite Include=`RuntimeFramework%09%(RuntimeFramework.Identity)%09%(RuntimeFramework.Version)`/>
      <LinesToWrite Include=`PackageDownload%09%(PackageDownload.Identity)%09%(PackageDownload.Version)`/>
      <LinesToWrite Include=`TargetingPack%09%(TargetingPack.Identity)%09%(TargetingPack.PackageVersion)`/>
      <LinesToWrite Include=`RuntimePack%09%(RuntimePack.Identity)%09%(RuntimePack.PackageVersion)`/>
      <LinesToWrite Include=`AppHostPack%09%(AppHostPack.Identity)%09%(AppHostPack.PackageVersion)`/>
    </ItemGroup>
    <WriteLinesToFile File=`$(OutputPath)resolvedversions.txt`
                      Lines=`@(LinesToWrite)`
                      Overwrite=`true`
                      Encoding=`Unicode`/>
    
  </Target>";
                    writeResolvedVersionsTarget = writeResolvedVersionsTarget.Replace('`', '"');

                    project.Root.Add(XElement.Parse(writeResolvedVersionsTarget));
                });

            if (projectChanges != null)
            {
                testAsset = testAsset.WithProjectChanges(projectChanges);
            }

            var command = new MSBuildCommand(Log, "WriteResolvedVersions", Path.Combine(testAsset.TestRoot, testProject.Name));

            command.Execute()
                .Should()
                .Pass();

            var outputDirectory = command.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: testProject.RuntimeIdentifier);
            var resolvedVersions = ResolvedVersionInfo.ParseFrom(Path.Combine(outputDirectory.FullName, "resolvedversions.txt"));

            return resolvedVersions;
        }

        private class ResolvedVersionInfo
        {
            public Dictionary<string, string> RuntimeFramework { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> PackageDownload { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> TargetingPack { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> RuntimePack { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> AppHostPack { get; } = new Dictionary<string, string>();

            public static ResolvedVersionInfo ParseFrom(string path)
            {
                var versionInfo = new ResolvedVersionInfo();
                foreach (var line in File.ReadAllLines(path))
                {
                    var fields = line.Split('\t');
                    if (fields.Length >= 3)
                    {
                        string itemType = fields[0];
                        string itemIdentity = fields[1];
                        string version = fields[2];
                        Dictionary<string, string> dict;
                        switch (itemType)
                        {
                            case "RuntimeFramework":
                                dict = versionInfo.RuntimeFramework;
                                break;
                            case "PackageDownload":
                                //  PackageDownload versions are enclosed in [brackets]
                                dict = versionInfo.PackageDownload;
                                version = version.Substring(1, version.Length - 2);
                                break;
                            case "TargetingPack":
                                dict = versionInfo.TargetingPack;
                                break;
                            case "RuntimePack":
                                dict = versionInfo.RuntimePack;
                                break;
                            case "AppHostPack":
                                dict = versionInfo.AppHostPack;
                                break;
                            default:
                                throw new InvalidOperationException("Unexpected item type: " + itemType);
                        }
                        dict[itemIdentity] = version;
                    }
                }
                return versionInfo;
            }
        }
    }
}
