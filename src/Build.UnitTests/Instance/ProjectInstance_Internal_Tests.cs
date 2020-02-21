// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Engine.UnitTests.TestComparers.ProjectInstanceModelTestComparers;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectInstance internal members
    /// </summary>
    public class ProjectInstance_Internal_Tests
    {
        private readonly ITestOutputHelper _output;

        public ProjectInstance_Internal_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Read task registrations
        /// </summary>
        [Fact]
        public void GetTaskRegistrations()
        {
            try
            {
                string projectFileContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='t0' AssemblyFile='af0'/>
                        <UsingTask TaskName='t1' AssemblyFile='af1a'/>
                        <ItemGroup>
                            <i Include='i0'/>
                        </ItemGroup>
                        <Import Project='{0}'/>
                    </Project>";

                string importContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='t1' AssemblyName='an1' Condition=""'$(p)'=='v'""/>
                        <UsingTask TaskName='t2' AssemblyName='an2' Condition=""'@(i)'=='i0'""/>
                        <UsingTask TaskName='t3' AssemblyFile='af' Condition='false'/>
                        <PropertyGroup>
                            <p>v</p>
                        </PropertyGroup>
                    </Project>";

                string importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.targets", importContent);
                projectFileContent = String.Format(projectFileContent, importPath);

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Assert.Equal(3, project.TaskRegistry.TaskRegistrations.Count);
                Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "af0"), project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t0", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyFile);
                Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "af1a"), project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t1", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyFile);
                Assert.Equal("an1", project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t1", null)][1].TaskFactoryAssemblyLoadInfo.AssemblyName);
                Assert.Equal("an2", project.TaskRegistry.TaskRegistrations[new TaskRegistry.RegisteredTaskIdentity("t2", null)][0].TaskFactoryAssemblyLoadInfo.AssemblyName);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// InitialTargets and DefaultTargets with imported projects.
        /// DefaultTargets are not read from imported projects.
        /// InitialTargets are gathered from imports depth-first.
        /// </summary>
        [Fact]
        public void InitialTargetsDefaultTargets()
        {
            try
            {
                string projectFileContent = @"
                    <Project DefaultTargets='d0a;d0b' InitialTargets='i0a;i0b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='{0}'/>
                        <Import Project='{1}'/>
                    </Project>";

                string import1Content = @"
                    <Project DefaultTargets='d1a;d1b' InitialTargets='i1a;i1b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='{0}'/>
                    </Project>";

                string import2Content = @"<Project DefaultTargets='d2a;2db' InitialTargets='i2a;i2b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";

                string import3Content = @"<Project DefaultTargets='d3a;d3b' InitialTargets='i3a;i3b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";

                string import2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.targets", import2Content);
                string import3Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import3.targets", import3Content);

                import1Content = String.Format(import1Content, import3Path);
                string import1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.targets", import1Content);

                projectFileContent = String.Format(projectFileContent, import1Path, import2Path);

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Helpers.AssertListsValueEqual(new string[] { "d0a", "d0b" }, project.DefaultTargets);
                Helpers.AssertListsValueEqual(new string[] { "i0a", "i0b", "i1a", "i1b", "i3a", "i3b", "i2a", "i2b" }, project.InitialTargets);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// InitialTargets and DefaultTargets with imported projects.
        /// DefaultTargets are not read from imported projects.
        /// InitialTargets are gathered from imports depth-first.
        /// </summary>
        [Fact]
        public void InitialTargetsDefaultTargetsEscaped()
        {
            try
            {
                string projectFileContent = @"
                    <Project DefaultTargets='d0a%3bd0b' InitialTargets='i0a%3bi0b' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    </Project>";

                ProjectInstance project = new Project(ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)))).CreateProjectInstance();

                Helpers.AssertListsValueEqual(new string[] { "d0a;d0b" }, project.DefaultTargets);
                Helpers.AssertListsValueEqual(new string[] { "i0a;i0b" }, project.InitialTargets);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Read property group under target
        /// </summary>
        [Fact]
        public void GetPropertyGroupUnderTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <PropertyGroup Condition='c1'>
                                <p1 Condition='c2'>v1</p1>
                                <p2/>
                            </PropertyGroup>
                        </Target>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content);
            ProjectPropertyGroupTaskInstance propertyGroup = (ProjectPropertyGroupTaskInstance)(p.Targets["t"].Children[0]);

            Assert.Equal("c1", propertyGroup.Condition);

            List<ProjectPropertyGroupTaskPropertyInstance> properties = Helpers.MakeList(propertyGroup.Properties);
            Assert.Equal(2, properties.Count);

            Assert.Equal("c2", properties[0].Condition);
            Assert.Equal("v1", properties[0].Value);

            Assert.Equal(String.Empty, properties[1].Condition);
            Assert.Equal(String.Empty, properties[1].Value);
        }

        /// <summary>
        /// Read item group under target
        /// </summary>
        [Fact]
        public void GetItemGroupUnderTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <ItemGroup Condition='c1'>
                                <i Include='i1' Exclude='e1' Condition='c2'>
                                    <m Condition='c3'>m1</m>    
                                    <n>n1</n>                        
                                </i>
                                <j Remove='r1'/>
                                <k>
                                    <o>o1</o>
                                </k>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content);
            ProjectItemGroupTaskInstance itemGroup = (ProjectItemGroupTaskInstance)(p.Targets["t"].Children[0]);

            Assert.Equal("c1", itemGroup.Condition);

            List<ProjectItemGroupTaskItemInstance> items = Helpers.MakeList(itemGroup.Items);
            Assert.Equal(3, items.Count);

            Assert.Equal("i1", items[0].Include);
            Assert.Equal("e1", items[0].Exclude);
            Assert.Equal(String.Empty, items[0].Remove);
            Assert.Equal("c2", items[0].Condition);

            Assert.Equal(String.Empty, items[1].Include);
            Assert.Equal(String.Empty, items[1].Exclude);
            Assert.Equal("r1", items[1].Remove);
            Assert.Equal(String.Empty, items[1].Condition);

            Assert.Equal(String.Empty, items[2].Include);
            Assert.Equal(String.Empty, items[2].Exclude);
            Assert.Equal(String.Empty, items[2].Remove);
            Assert.Equal(String.Empty, items[2].Condition);

            List<ProjectItemGroupTaskMetadataInstance> metadata1 = Helpers.MakeList(items[0].Metadata);
            List<ProjectItemGroupTaskMetadataInstance> metadata2 = Helpers.MakeList(items[1].Metadata);
            List<ProjectItemGroupTaskMetadataInstance> metadata3 = Helpers.MakeList(items[2].Metadata);

            Assert.Equal(2, metadata1.Count);
            Assert.Empty(metadata2);
            Assert.Single(metadata3);

            Assert.Equal("c3", metadata1[0].Condition);
            Assert.Equal("m1", metadata1[0].Value);
            Assert.Equal(String.Empty, metadata1[1].Condition);
            Assert.Equal("n1", metadata1[1].Value);

            Assert.Equal(String.Empty, metadata3[0].Condition);
            Assert.Equal("o1", metadata3[0].Value);
        }

        /// <summary>
        /// Task registry accessor
        /// </summary>
        [Fact]
        public void GetTaskRegistry()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.True(p.TaskRegistry != null);
        }

        /// <summary>
        /// Global properties accessor
        /// </summary>
        [Fact]
        public void GetGlobalProperties()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.Equal("v1", p.GlobalPropertiesDictionary["g1"].EvaluatedValue);
            Assert.Equal("v2", p.GlobalPropertiesDictionary["g2"].EvaluatedValue);
        }

        /// <summary>
        /// ToolsVersion accessor
        /// </summary>
        [Fact]
        public void GetToolsVersion()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, p.Toolset.ToolsVersion);
        }

        [Fact]
        public void UsingExplicitToolsVersionShouldBeFalseWhenNoToolsetIsReferencedInProject()
        {
            var projectInstance = new ProjectInstance(
                new ProjectRootElement(
                    XmlReader.Create(new StringReader("<Project></Project>")), ProjectCollection.GlobalProjectCollection.ProjectRootElementCache, false, false)
                );

            projectInstance.UsingDifferentToolsVersionFromProjectFile.ShouldBeFalse();

        }

        /// <summary>
        /// Toolset data is cloned properly
        /// </summary>
        [Fact]
        public void CloneToolsetData()
        {
            var projectCollection = new ProjectCollection();
            CreateMockToolsetIfNotExists("TESTTV", projectCollection);
            ProjectInstance first = GetSampleProjectInstance(null, null, projectCollection, toolsVersion: "TESTTV");
            ProjectInstance second = first.DeepCopy();
            Assert.Equal(first.ToolsVersion, second.ToolsVersion);
            Assert.Equal(first.ExplicitToolsVersion, second.ExplicitToolsVersion);
            Assert.Equal(first.ExplicitToolsVersionSpecified, second.ExplicitToolsVersionSpecified);
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                ProjectInstance p = GetSampleProjectInstance(null, null, new ProjectCollection());

                Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, p.Toolset.ToolsVersion);
                Assert.Equal(p.Toolset.DefaultSubToolsetVersion, p.SubToolsetVersion);

                if (p.Toolset.DefaultSubToolsetVersion == null)
                {
                    Assert.Equal(MSBuildConstants.CurrentVisualStudioVersion, p.GetPropertyValue("VisualStudioVersion"));
                }
                else
                {
                    Assert.Equal(p.Toolset.DefaultSubToolsetVersion, p.GetPropertyValue("VisualStudioVersion"));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version when it is overridden by a value in the 
        /// environment 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void GetSubToolsetVersion_FromEnvironment()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABCD");

                ProjectInstance p = GetSampleProjectInstance(null, null, new ProjectCollection());

                Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, p.Toolset.ToolsVersion);
                Assert.Equal("ABCD", p.SubToolsetVersion);
                Assert.Equal("ABCD", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version when it is overridden by a global property
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion_FromProjectGlobalProperties()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCDE");

                ProjectInstance p = GetSampleProjectInstance(null, globalProperties, new ProjectCollection());

                Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, p.Toolset.ToolsVersion);
                Assert.Equal("ABCDE", p.SubToolsetVersion);
                Assert.Equal("ABCDE", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Verify that if a sub-toolset version is passed to the constructor, it all other heuristic methods for 
        /// getting the sub-toolset version. 
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion_FromConstructor()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABC");

                string projectContent = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t'>
                            <Message Text='Hello'/>
                        </Target>
                    </Project>";

                ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContent)));

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCD");

                IDictionary<string, string> projectCollectionGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                projectCollectionGlobalProperties.Add("VisualStudioVersion", "ABCDE");

                ProjectInstance p = new ProjectInstance(xml, globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion, "ABCDEF", new ProjectCollection(projectCollectionGlobalProperties));

                Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, p.Toolset.ToolsVersion);
                Assert.Equal("ABCDEF", p.SubToolsetVersion);
                Assert.Equal("ABCDEF", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// DefaultTargets accessor
        /// </summary>
        [Fact]
        public void GetDefaultTargets()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Helpers.AssertListsValueEqual(new string[] { "dt" }, p.DefaultTargets);
        }

        /// <summary>
        /// InitialTargets accessor
        /// </summary>
        [Fact]
        public void GetInitialTargets()
        {
            ProjectInstance p = GetSampleProjectInstance();

            Helpers.AssertListsValueEqual(new string[] { "it" }, p.InitialTargets);
        }

        /// <summary>
        /// Cloning project clones targets
        /// </summary>
        [Fact]
        public void CloneTargets()
        {
            var hostServices = new HostServices();

            ProjectInstance first = GetSampleProjectInstance(hostServices);
            ProjectInstance second = first.DeepCopy();

            // Targets, tasks are immutable so we can expect the same objects
            Assert.True(Object.ReferenceEquals(first.Targets, second.Targets));
            Assert.True(Object.ReferenceEquals(first.Targets["t"], second.Targets["t"]));

            var firstTasks = first.Targets["t"];
            var secondTasks = second.Targets["t"];

            Assert.True(Object.ReferenceEquals(firstTasks.Children[0], secondTasks.Children[0]));
        }

        /// <summary>
        /// Cloning project copies task registry
        /// </summary>
        [Fact]
        public void CloneTaskRegistry()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            // Task registry object should be immutable
            Assert.Same(first.TaskRegistry, second.TaskRegistry);
        }

        /// <summary>
        /// Cloning project copies global properties
        /// </summary>
        [Fact]
        public void CloneGlobalProperties()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.Equal("v1", second.GlobalPropertiesDictionary["g1"].EvaluatedValue);
            Assert.Equal("v2", second.GlobalPropertiesDictionary["g2"].EvaluatedValue);
        }

        /// <summary>
        /// Cloning project copies default targets
        /// </summary>
        [Fact]
        public void CloneDefaultTargets()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Helpers.AssertListsValueEqual(new string[] { "dt" }, second.DefaultTargets);
        }

        /// <summary>
        /// Cloning project copies initial targets
        /// </summary>
        [Fact]
        public void CloneInitialTargets()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Helpers.AssertListsValueEqual(new string[] { "it" }, second.InitialTargets);
        }

        /// <summary>
        /// Cloning project copies toolsversion
        /// </summary>
        [Fact]
        public void CloneToolsVersion()
        {
            ProjectInstance first = GetSampleProjectInstance();
            ProjectInstance second = first.DeepCopy();

            Assert.Equal(first.Toolset, second.Toolset);
        }
        
        /// <summary>
        /// Cloning project copies toolsversion
        /// </summary>
        [Fact]
        public void CloneStateTranslation()
        {
            ProjectInstance first = GetSampleProjectInstance();
            first.TranslateEntireState = true;

            ProjectInstance second = first.DeepCopy();

            Assert.True(second.TranslateEntireState);
        }

        /// <summary>
        /// Tests building a simple project and verifying the log looks as expected.
        /// </summary>
        [Fact]
        public void Build()
        {
            // Setting the current directory to the MSBuild running location. It *should* be this
            // already, but if it's not some other test changed it and didn't change it back. If
            // the directory does not include the reference dlls the compilation will fail.
            Directory.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);

            string projectFileContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <UsingTask TaskName='Microsoft.Build.Tasks.Message' AssemblyFile='Microsoft.Build.Tasks.Core.dll'/>
                        <ItemGroup>
                            <i Include='i0'/>
                        </ItemGroup>
                        <Target Name='Build'>
                            <Message Text='Building...'/>
                            <Message Text='Completed!'/>
                        </Target>
                    </Project>";

            ProjectInstance projectInstance = GetProjectInstance(projectFileContent);
            List<ILogger> loggers = new List<ILogger>();
            MockLogger mockLogger = new MockLogger(_output);
            loggers.Add(mockLogger);
            bool success = projectInstance.Build("Build", loggers);

            Assert.True(success);
            mockLogger.AssertLogContains(new string[] { "Building...", "Completed!" });
        }

        [Theory]
        [InlineData(
            @"      <Project>
                    </Project>
                ")]
        // Project with one of each direct child(indirect children trees are tested separately)
        [InlineData(
            @"      <Project InitialTargets=`t1` DefaultTargets=`t2` ToolsVersion=`{0}`>
                        <UsingTask TaskName=`t1` AssemblyFile=`f1`/>

                        <ItemDefinitionGroup>
                            <i>
                              <n>n1</n>
                            </i>
                        </ItemDefinitionGroup>

                        <PropertyGroup>
                            <p1>v1</p1>
                        </PropertyGroup>

                        <ItemGroup>
                            <i Include='i0'/>
                        </ItemGroup>

                        <Target Name='t1'>
                            <t1/>
                        </Target>

                        <Target Name='t2' BeforeTargets=`t1`>
                            <t2/>
                        </Target>

                        <Target Name='t3' AfterTargets=`t2`>
                            <t3/>
                        </Target>
                    </Project>
                ")]
        // Project with at least two instances of each direct child. Tests that collections serialize well.
        [InlineData(
            @"      <Project InitialTargets=`t1` DefaultTargets=`t2` ToolsVersion=`{0}`>
                        <UsingTask TaskName=`t1` AssemblyFile=`f1`/>
                        <UsingTask TaskName=`t2` AssemblyFile=`f2`/>

                        <ItemDefinitionGroup>
                            <i>
                              <n>n1</n>
                            </i>
                        </ItemDefinitionGroup>

                        <ItemDefinitionGroup>
                            <i2>
                              <n2>n2</n2>
                            </i2>
                        </ItemDefinitionGroup>

                        <PropertyGroup>
                            <p1>v1</p1>
                        </PropertyGroup>

                        <PropertyGroup>
                            <p2>v2</p2>
                        </PropertyGroup>

                        <ItemGroup>
                            <i Include='i1'/>
                        </ItemGroup>

                        <ItemGroup>
                            <i2 Include='i2'>
                              <m1 Condition=`1==1`>m1</m1>
                              <m2>m2</m2>
                            </i2>
                        </ItemGroup>

                        <Target Name='t1'>
                            <t1/>
                        </Target>

                        <Target Name='t2' BeforeTargets=`t1`>
                            <t2/>
                        </Target>

                        <Target Name='t3' AfterTargets=`t1`>
                            <t3/>
                        </Target>

                        <Target Name='t4' BeforeTargets=`t1`>
                            <t4/>
                        </Target>

                        <Target Name='t5' AfterTargets=`t1`>
                            <t5/>
                        </Target>
                    </Project>
                ")]
        public void ProjectInstanceCanSerializeEntireStateViaTranslator(string projectContents)
        {
            projectContents = string.Format(projectContents, MSBuildConstants.CurrentToolsVersion);

            var original = new ProjectInstance(ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(projectContents)))));

            original.TranslateEntireState = true;

            ((ITranslatable) original).Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ProjectInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, copy, new ProjectInstanceComparer());
        }

        public delegate ProjectInstance ProjectInstanceFactory(string file, ProjectRootElement xml, ProjectCollection collection);

        public static IEnumerable<object[]> ProjectInstanceHasEvaluationIdTestData()
        {
            // from file (new)
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => new ProjectInstance(f, null, null, c)
            };

            // from file (factory method)
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => ProjectInstance.FromFile(f, new ProjectOptions { ProjectCollection = c })
            };

            // from Project
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => new Project(f, null, null, c).CreateProjectInstance()
            };

            // from DeepCopy
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => new ProjectInstance(f, null, null, c).DeepCopy()
            };

            // from ProjectRootElement (new)
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => new ProjectInstance(xml, null, null, c)
            };

            // from ProjectRootElement (factory method)
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) => ProjectInstance.FromProjectRootElement(xml, new ProjectOptions { ProjectCollection = c })
            };

            // from translated project instance
            yield return new ProjectInstanceFactory[]
            {
                (f, xml, c) =>
                {
                    var pi = new ProjectInstance(f, null, null, c);
                    pi.AddItem("foo", "bar");
                    pi.TranslateEntireState = true;

                    ((ITranslatable) pi).Translate(TranslationHelpers.GetWriteTranslator());
                    var copy = ProjectInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

                    return copy;
                }
            };
        }

        [Theory]
        [MemberData(nameof(ProjectInstanceHasEvaluationIdTestData))]
        public void ProjectInstanceHasEvaluationId(ProjectInstanceFactory projectInstanceFactory)
        {
            using (var env = TestEnvironment.Create())
            {
                var file = env.CreateFile().Path;
                var projectCollection = env.CreateProjectCollection().Collection;

                var xml = ProjectRootElement.Create(projectCollection);
                xml.Save(file);

                var projectInstance = projectInstanceFactory.Invoke(file, xml, projectCollection);
                Assert.NotEqual(BuildEventContext.InvalidEvaluationId, projectInstance.EvaluationId);
            }
        }

        [Fact]
        public void AddTargetAddsNewTarget()
        {
            string projectFileContent = @"
                    <Project>
                        <Target Name='a' />
                    </Project>";
            ProjectRootElement rootElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            ProjectInstance projectInstance = new ProjectInstance(rootElement);

            ProjectTargetInstance targetInstance = projectInstance.AddTarget("b", "1==1", "inputs", "outputs", "returns", "keepDuplicateOutputs", "dependsOnTargets", "beforeTargets", "afterTargets", true);

            Assert.Equal(2, projectInstance.Targets.Count);
            Assert.Equal(targetInstance, projectInstance.Targets["b"]);
            Assert.Equal("b", targetInstance.Name);
            Assert.Equal("1==1", targetInstance.Condition);
            Assert.Equal("inputs", targetInstance.Inputs);
            Assert.Equal("outputs", targetInstance.Outputs);
            Assert.Equal("returns", targetInstance.Returns);
            Assert.Equal("keepDuplicateOutputs", targetInstance.KeepDuplicateOutputs);
            Assert.Equal("dependsOnTargets", targetInstance.DependsOnTargets);
            Assert.Equal("beforeTargets", targetInstance.BeforeTargets);
            Assert.Equal("afterTargets", targetInstance.AfterTargets);
            Assert.Equal(projectInstance.ProjectFileLocation, targetInstance.Location);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.ConditionLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.InputsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.OutputsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.ReturnsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.KeepDuplicateOutputsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.DependsOnTargetsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.BeforeTargetsLocation);
            Assert.Equal(ElementLocation.EmptyLocation, targetInstance.AfterTargetsLocation);
            Assert.True(targetInstance.ParentProjectSupportsReturnsAttribute);
        }

        [Fact]
        public void AddTargetThrowsWithExistingTarget()
        {
            string projectFileContent = @"
                    <Project>
                        <Target Name='a' />
                    </Project>";
            ProjectRootElement rootElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            ProjectInstance projectInstance = new ProjectInstance(rootElement);

            Assert.Throws<InternalErrorException>(() => projectInstance.AddTarget("a", "1==1", "inputs", "outputs", "returns", "keepDuplicateOutputs", "dependsOnTargets", "beforeTargets", "afterTargets", true));
        }

        [Theory]
        [InlineData(false, ProjectLoadSettings.Default)]
        [InlineData(false, ProjectLoadSettings.RecordDuplicateButNotCircularImports)]
        [InlineData(true, ProjectLoadSettings.Default)]
        [InlineData(true, ProjectLoadSettings.RecordDuplicateButNotCircularImports)]
        public void GetImportPathsAndImportPathsIncludingDuplicates(bool useDirectConstruction, ProjectLoadSettings projectLoadSettings)
        {
            try
            {
                string projectFileContent = @"
                    <Project>
                        <Import Project='{0}'/>
                        <Import Project='{1}'/>
                        <Import Project='{0}'/>
                    </Project>";

                string import1Content = @"
                    <Project>
                        <Import Project='{0}'/>
                        <Import Project='{1}'/>
                    </Project>";

                string import2Content = @"<Project />";
                string import3Content = @"<Project />";

                string import2Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.targets", import2Content);
                string import3Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import3.targets", import3Content);

                import1Content = string.Format(import1Content, import2Path, import3Path);
                string import1Path = ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.targets", import1Content);

                projectFileContent = string.Format(projectFileContent, import1Path, import2Path);

                ProjectCollection projectCollection = new ProjectCollection();
                BuildParameters buildParameters = new BuildParameters(projectCollection) { ProjectLoadSettings = projectLoadSettings };
                BuildEventContext buildEventContext = new BuildEventContext(0, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

                ProjectRootElement rootElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
                ProjectInstance projectInstance = useDirectConstruction
                    ? new ProjectInstance(rootElement, globalProperties: null, toolsVersion: null, buildParameters, projectCollection.LoggingService, buildEventContext, sdkResolverService: null, 0)
                    : new Project(rootElement, globalProperties: null, toolsVersion: null, projectCollection, projectLoadSettings).CreateProjectInstance();

                string[] expectedImportPaths = new string[] { import1Path, import2Path, import3Path };
                string[] expectedImportPathsIncludingDuplicates = projectLoadSettings.HasFlag(ProjectLoadSettings.RecordDuplicateButNotCircularImports)
                    ? new string[] { import1Path, import2Path, import3Path, import2Path, import1Path }
                    : expectedImportPaths;

                Helpers.AssertListsValueEqual(expectedImportPaths, projectInstance.ImportPaths.ToList());
                Helpers.AssertListsValueEqual(expectedImportPathsIncludingDuplicates, projectInstance.ImportPathsIncludingDuplicates.ToList());
            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content)
        {
            return GetProjectInstance(content, null);
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content and host services object
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content, HostServices hostServices)
        {
            return GetProjectInstance(content, hostServices, null, null);
        }

        /// <summary>
        /// Create a ProjectInstance from provided project content and host services object
        /// </summary>
        private static ProjectInstance GetProjectInstance(string content, HostServices hostServices, IDictionary<string, string> globalProperties, ProjectCollection projectCollection, string toolsVersion = null)
        {
            XmlReader reader = XmlReader.Create(new StringReader(content));

            if (globalProperties == null)
            {
                // choose some interesting defaults if we weren't explicitly asked to use a set. 
                globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("g1", "v1");
                globalProperties.Add("g2", "v2");
            }

            Project project = new Project(reader, globalProperties, toolsVersion ?? ObjectModelHelpers.MSBuildDefaultToolsVersion, projectCollection ?? ProjectCollection.GlobalProjectCollection);

            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance()
        {
            return GetSampleProjectInstance(null);
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance(HostServices hostServices)
        {
            return GetSampleProjectInstance(hostServices, null, null);
        }

        /// <summary>
        /// Create a ProjectInstance with some items and properties and targets
        /// </summary>
        private static ProjectInstance GetSampleProjectInstance(HostServices hostServices, IDictionary<string, string> globalProperties, ProjectCollection projectCollection, string toolsVersion = null)
        {
            string toolsVersionSubstring = toolsVersion != null ? "ToolsVersion=\"" + toolsVersion + "\" " : String.Empty;
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' InitialTargets='it' DefaultTargets='dt' " + toolsVersionSubstring + @">
                        <PropertyGroup>
                            <p1>v1</p1>
                            <p2>v2</p2>
                            <p2>$(p2)X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Include='i0'/>
                            <i Include='i1'>
                                <m>m1</m>
                            </i>
                            <i Include='$(p1)'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <t1 a='a1' b='b1' ContinueOnError='coe' Condition='c'/>
                            <t2/>
                        </Target>
                        <Target Name='tt'/>
                    </Project>
                ";

            ProjectInstance p = GetProjectInstance(content, hostServices, globalProperties, projectCollection, toolsVersion);

            return p;
        }

        /// <summary>
        /// Creates a toolset with the given tools version if one does not already exist.
        /// </summary>
        private static void CreateMockToolsetIfNotExists(string toolsVersion, ProjectCollection projectCollection)
        {
            ProjectCollection pc = projectCollection;
            if (!pc.Toolsets.Any(t => String.Equals(t.ToolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase)))
            {
                Toolset template = pc.Toolsets.First(t => String.Equals(t.ToolsVersion, pc.DefaultToolsVersion, StringComparison.OrdinalIgnoreCase));
                var toolset = new Toolset(
                    toolsVersion,
                    template.ToolsPath,
                    template.Properties.ToDictionary(p => p.Key, p => p.Value.EvaluatedValue),
                    pc,
                    null);
                pc.AddToolset(toolset);
            }
        }
    }
}
