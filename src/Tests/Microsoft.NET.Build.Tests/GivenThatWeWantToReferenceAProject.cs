// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToReferenceAProject : SdkTest
    {
        const string tfm = ToolsetInfo.CurrentTargetFramework;
        public GivenThatWeWantToReferenceAProject(ITestOutputHelper log) : base(log)
        {
        }

        //  Different types of projects which should form the test matrix:

        //  Desktop (non-SDK) project
        //  Single-targeted SDK project
        //  Multi-targeted SDK project
        //  PCL

        //  Compatible
        //  Incompatible

        //  Exe
        //  Library

        //  .NET Core
        //  .NET Standard
        //  .NET Framework (SDK and non-SDK)

        public enum ReferenceBuildResult
        {
            BuildSucceeds,
            FailsRestore,
            FailsBuild
        }

        [RequiresMSBuildVersionTheory("16.8.0.42407")]
        [InlineData("net5.0-windows", "net5.0", true)]
        [InlineData("net5.0", "net5.0-windows", false)]
        [InlineData("net5.0-windows", "net5.0-windows", true)]
        [InlineData("net5.0-windows", "net5.0-windows7.0", true)]
        [InlineData("net5.0-windows7.0", "net5.0-windows", true)]
        public void It_checks_for_valid_platform_references(string referencerTarget, string dependencyTarget, bool succeeds)
        {
            It_checks_for_valid_references(referencerTarget, true, dependencyTarget, true, succeeds, succeeds);
        }

        [Theory]
        [InlineData("netstandard1.2", true, "netstandard1.5", true, false, false)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}", true, "net45;netstandard1.5", true, true, true)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}", true, "net45;net46", true, true, true)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework};net462", true, "netstandard1.4", true, true, true)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework};net45", true, "netstandard1.4", true, false, false)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework};net46", true, "net45;netstandard1.6", true, true, true)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework};net45", true, "net46;netstandard1.6", true, false, false)]
        [InlineData("v4.5.2", false, "netstandard1.6", true, true, false)]
        [InlineData("v4.7.2", false, "netstandard1.6;net472", true, true, true)]
        [InlineData("v4.5.2", false, "netstandard1.6;net472", true, true, false)]
        public void It_checks_for_valid_references(string referencerTarget, bool referencerIsSdkProject,
            string dependencyTarget, bool dependencyIsSdkProject,
            bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = referencerTarget.ToString() + " " + dependencyTarget.ToString();
            //  MSBuild isn't happy with semicolons in the path when doing file exists checks
            identifier = identifier.Replace(';', '_');

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, referencerIsSdkProject);
            TestProject dependencyProject = GetTestProject("Dependency", dependencyTarget, dependencyIsSdkProject);
            referencerProject.ReferencedProjects.Add(dependencyProject);

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.TargetFrameworkIdentifiers.Contains(ConstantStringValues.NetstandardTargetFrameworkIdentifier))
            {
                referencerProject.IsExe = true;
            }

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!referencerProject.BuildsOnNonWindows || !dependencyProject.BuildsOnNonWindows)
                {
                    return;
                }
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(It_checks_for_valid_references), identifier);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: "Referencer");

            if (restoreSucceeds)
            {
                restoreCommand
                    .Execute()
                    .Should()
                    .Pass();
            }
            else
            {
                restoreCommand
                    .Execute()
                    .Should()
                    .Fail();
            }

            if (!referencerProject.IsSdkProject)
            {
                //  The Restore target currently seems to be a no-op for non-SDK projects,
                //  so we need to explicitly restore the dependency
                testAsset.GetRestoreCommand(Log, relativePath: "Dependency")
                    .Execute()
                    .Should()
                    .Pass();
            }

            var buildCommand = new BuildCommand(testAsset, "Referencer");
            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else if (referencerIsSdkProject)
            {
                result.Should().Fail().And.HaveStdOutContaining("NU1201");
            }
            else
            {
                result.Should().Fail()
                    .And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }
        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }

        [RequiresMSBuildVersionTheory("16.7.1")]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void It_disables_copying_conflicting_transitive_content(bool copyConflictingTransitiveContent, bool explicitlySet)
        {
            var contentName = "script.sh";
            var childProject = new TestProject()
            {
                TargetFrameworks = tfm,
                Name = "ChildProject",
            };
            var childAsset = _testAssetsManager.CreateTestProject(childProject, identifier: copyConflictingTransitiveContent.ToString() + explicitlySet.ToString())
                .WithProjectChanges(project => AddProjectChanges(project));
            File.WriteAllText(Path.Combine(childAsset.Path, childProject.Name, contentName), childProject.Name);

            var parentProject = new TestProject()
            {
                TargetFrameworks = tfm,
                Name = "ParentProject",
            };
            if (explicitlySet)
            {
                parentProject.AdditionalProperties["CopyConflictingTransitiveContent"] = copyConflictingTransitiveContent.ToString().ToLower();
            }
            var parentAsset = _testAssetsManager.CreateTestProject(parentProject, identifier: copyConflictingTransitiveContent.ToString() + explicitlySet.ToString())
                .WithProjectChanges(project => AddProjectChanges(project, Path.Combine(childAsset.Path, childProject.Name, childProject.Name + ".csproj")));
            File.WriteAllText(Path.Combine(parentAsset.Path, parentProject.Name, contentName), parentProject.Name);

            var buildCommand = new BuildCommand(parentAsset);
            buildCommand.Execute().Should().Pass();

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(parentAsset.Path, parentProject.Name), tfm, "ResultOutput")
            {
                DependsOnTargets = "Build"
            };
            getValuesCommand.Execute().Should().Pass();

            var valuesResult = getValuesCommand.GetValuesWithMetadata().Select(pair => Path.GetFullPath(pair.value));
            if (copyConflictingTransitiveContent)
            {
                valuesResult.Count().Should().Be(2);
                valuesResult.Should().BeEquivalentTo(Path.GetFullPath(Path.Combine(parentAsset.Path, parentProject.Name, contentName)),
                                                     Path.GetFullPath(Path.Combine(childAsset.Path, childProject.Name, contentName)));
            }
            else
            {
                valuesResult.Count().Should().Be(1);
                valuesResult.First().Should().Contain(Path.GetFullPath(Path.Combine(parentAsset.Path, parentProject.Name, contentName)));
            }
        }

        private void AddProjectChanges(XDocument project, string childPath = null)
        {
            var ns = project.Root.Name.Namespace;

            var itemGroup = new XElement(ns + "ItemGroup");
            project.Root.Add(itemGroup);

            var content = new XElement(ns + "Content",
                new XAttribute("Include", "script.sh"), new XAttribute("CopyToOutputDirectory", "PreserveNewest"));
            itemGroup.Add(content);

            if (childPath != null)
            {
                var projRef = new XElement(ns + "ProjectReference",
                    new XAttribute("Include", childPath));
                itemGroup.Add(projRef);

                var target = new XElement(ns + "Target",
                    new XAttribute("Name", "WriteOutput"),
                    new XAttribute("DependsOnTargets", "GetCopyToOutputDirectoryItems"),
                    new XAttribute("BeforeTargets", "_CopyOutOfDateSourceItemsToOutputDirectory"));

                var propertyGroup = new XElement(ns + "PropertyGroup");
                propertyGroup.Add(new XElement("ResultOutput", "@(AllItemsFullPathWithTargetPath)"));
                target.Add(propertyGroup);
                project.Root.Add(target);
            }
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_copies_content_transitively()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testProjectA = new TestProject()
            {
                Name = "ProjectA",
                TargetFrameworks = targetFramework,
            };

            var testProjectB = new TestProject()
            {
                Name = "ProjectB",
                TargetFrameworks = targetFramework,
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            var testProjectC = new TestProject()
            {
                Name = "ProjectC",
                TargetFrameworks = targetFramework,
            };
            testProjectC.AdditionalProperties.Add("DisableTransitiveProjectReferences", "true");
            testProjectC.ReferencedProjects.Add(testProjectB);
            var testAsset = _testAssetsManager.CreateTestProject(testProjectC).WithProjectChanges((path, p) =>
            {
                if (Path.GetFileNameWithoutExtension(path) == testProjectA.Name)
                {
                    var ns = p.Root.Name.Namespace;
                    p.Root.Add(new XElement(ns + "ItemGroup",
                        new XElement(ns + "Content", new XAttribute("Include", "a.txt"), new XAttribute("CopyToOutputDirectory", "PreserveNewest"))));
                }
            });
            File.WriteAllText(Path.Combine(testAsset.Path, testProjectA.Name, "a.txt"), "A");

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var contentPath = Path.Combine(testProjectC.GetOutputDirectory(testAsset.Path), "a.txt");
            File.Exists(contentPath).Should().BeTrue();
            var binDir = new DirectoryInfo(Path.GetDirectoryName(contentPath));
            binDir.Delete(true);

            buildCommand
                .Execute("/p:BuildProjectReferences=false")
                .Should()
                .Pass();

            File.Exists(contentPath).Should().BeTrue();
        }

        [Fact]
        public void It_conditionally_references_project_based_on_tfm()
        {
            var testProjectA = new TestProject()
            {
                Name = "ProjectA",
                TargetFrameworks = "netstandard2.1"
            };

            var testProjectB = new TestProject()
            {
                Name = "ProjectB",
                TargetFrameworks = "netstandard2.1"
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            string source = @"using System;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(ProjectA.ProjectAClass.Name);
    }
}";
            var testProjectC = new TestProject()
            {
                Name = "ProjectC",
                IsExe = true,
                TargetFrameworks = $"netstandard2.1;{tfm}"
            };
            testProjectC.ReferencedProjects.Add(testProjectB);
            testProjectC.SourceFiles.Add("Program.cs", source);

            var testAsset = _testAssetsManager.CreateTestProject(testProjectC).WithProjectChanges((path, p) =>
            {
                if (Path.GetFileName(path).Equals("ProjectC.csproj"))
                {
                    var ns = p.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup",
                        new XAttribute("Condition", $@"'$(TargetFramework)' == '{tfm}'"));
                    var projRef = new XElement(ns + "ProjectReference",
                        new XAttribute("Include", Path.Combine(path, "..", "..", testProjectA.Name, $"{testProjectA.Name}.csproj")));
                    itemGroup.Add(projRef);
                    p.Root.Add(itemGroup);
                }
            });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
