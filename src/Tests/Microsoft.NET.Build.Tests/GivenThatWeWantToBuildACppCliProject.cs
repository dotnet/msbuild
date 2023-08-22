// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACppCliProject : SdkTest
    {
        public GivenThatWeWantToBuildACppCliProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void It_builds_and_runs()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) => AddBuildProperty(projectPath, project, "EnableManagedpackageReferenceSupport", "true"));

            // build projects separately with BuildProjectReferences=false to simulate VS build behavior
            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset, "CSConsoleApp");
            buildCommand
                .Execute(new string[] { "-p:Platform=x64", "-p:BuildProjectReferences=false" })
                .Should()
                .Pass();

            var exe = Path.Combine(
                //find the platform directory
                new DirectoryInfo(Path.Combine(testAsset.TestRoot, "CSConsoleApp", "bin")).GetDirectories().Single().FullName,
                "Debug",
                $"{ToolsetInfo.CurrentTargetFramework}-windows",
                "CSConsoleApp.exe");

            var runCommand = new RunExeCommand(Log, exe);
            runCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, World!");
        }

        [FullMSBuildOnlyFact]
        public void It_builds_and_runs_with_package_reference()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework + "-windows";
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) => ConfigureProject(projectPath, project, targetFramework, new string[] { "_EnablePackageReferencesInVCProjects", "IncludeWindowsSDKRefFrameworkReferences" }));

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();

            var cppnProjProperties = GetPropertyValues(testAsset.TestRoot, "NETCoreCppCliTest", targetFramework: targetFramework);
            Assert.True(cppnProjProperties["_EnablePackageReferencesInVCProjects"] == "true");
            Assert.True(cppnProjProperties["IncludeWindowsSDKRefFrameworkReferences"] == "");
        }

        [FullMSBuildOnlyFact]
        public void Given_no_restore_It_builds_cpp_project()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) => AddBuildProperty(projectPath, project, "EnableManagedpackageReferenceSupport", "True")); ;

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();
        }

        [FullMSBuildOnlyFact]
        public void Given_Wpf_framework_reference_It_builds_cpp_project()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CppCliLibWithWpfFrameworkReference")
                .WithSource();

            new BuildCommand(testAsset)
                .Execute("-p:Platform=x64")
                .Should()
                .Pass();
        }

        [FullMSBuildOnlyFact]
        public void It_fails_with_error_message_on_EnableComHosting()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetExtension(projectPath) == ".vcxproj")
                    {
                        XNamespace ns = project.Root.Name.Namespace;

                        var globalPropertyGroup = project.Root
                            .Descendants(ns + "PropertyGroup")
                            .Where(e => e.Attribute("Label")?.Value == "Globals")
                            .Single();
                        globalPropertyGroup.Add(new XElement(ns + "EnableComHosting", "true"));
                    }
                });

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NoSupportCppEnableComHosting);
        }

        [FullMSBuildOnlyFact]
        public void It_fails_with_error_message_on_fullframework()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                    ChangeTargetFramework(projectPath, project, "net472"));

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NETFrameworkWithoutUsingNETSdkDefaults);
        }

        [FullMSBuildOnlyFact]
        public void It_fails_with_error_message_on_tfm_lower_than_3_1()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                    ChangeTargetFramework(projectPath, project, "netcoreapp3.0"));

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CppRequiresTFMVersion31);
        }

        [FullMSBuildOnlyFact]
        public void When_run_with_selfcontained_It_fails_with_error_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new BuildCommand(testAsset, "NETCoreCppCliTest")
                .Execute("-p:Platform=x64", "-p:selfcontained=true", $"-p:RuntimeIdentifier={ToolsetInfo.LatestWinRuntimeIdentifier}-x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NoSupportCppSelfContained);
        }

        private void ChangeTargetFramework(string projectPath, XDocument project, string targetFramework)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                XNamespace ns = project.Root.Name.Namespace;

                project.Root.Descendants(ns + "PropertyGroup")
                                            .Descendants(ns + "TargetFramework")
                                            .Single().Value = targetFramework;
            }
        }

        private void ConfigureProject(string projectPath, XDocument project, string targetFramework, string[] properties)
        {
            AddBuildProperty(projectPath, project, "EnableManagedpackageReferenceSupport", "true");
            ChangeTargetFramework(projectPath, project, targetFramework);
            RecordProperties(projectPath, project, properties);
        }

        private void AddBuildProperty(string projectPath, XDocument project, string property, string value)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                XNamespace ns = project.Root.Name.Namespace;
                XElement propertyGroup = project.Root.Descendants(ns + "PropertyGroup").First();
                propertyGroup.Add(new XElement(ns + $"{property}", value));
            }
        }
        private void RecordProperties(string projectPath, XDocument project, string[] properties)
        {
            if (Path.GetExtension(projectPath) == ".vcxproj")
            {
                string propertiesTextElements = "";
                XNamespace ns = project.Root.Name.Namespace;
                foreach (var propertyName in properties)
                {
                    propertiesTextElements += $"      <LinesToWrite Include='{propertyName}: $({propertyName})'/>" + Environment.NewLine;
                }

                string target = $@"<Target Name='WritePropertyValues' BeforeTargets='AfterBuild'>
                    <ItemGroup>
                {propertiesTextElements}
                    </ItemGroup>
                    <WriteLinesToFile
                      File='$(BaseIntermediateOutputPath)\$(Configuration)\$(TargetFramework)\PropertyValues.txt'
                      Lines='@(LinesToWrite)'
                      Overwrite='true'
                      Encoding='Unicode'
                      />
                  </Target>";
                XElement newNode = XElement.Parse(target);
                foreach (var element in newNode.DescendantsAndSelf())
                {
                    element.Name = ns + element.Name.LocalName;
                }
                project.Root.AddFirst(newNode);
            }
        }

        public Dictionary<string, string> GetPropertyValues(string testRoot, string project, string targetFramework = null, string configuration = "Debug")
        {
            var propertyValues = new Dictionary<string, string>();

            string intermediateOutputPath = Path.Combine(testRoot, project, "obj", configuration, targetFramework ?? "foo");

            foreach (var line in File.ReadAllLines(Path.Combine(intermediateOutputPath, "PropertyValues.txt")))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    string propertyName = line.Substring(0, colonIndex);
                    string propertyValue = line.Length == colonIndex + 1 ? "" : line.Substring(colonIndex + 2);
                    propertyValues[propertyName] = propertyValue;
                }
            }

            return propertyValues;
        }
    }
}
