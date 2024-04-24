// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectTargetElement class
    /// </summary>
    public class ProjectTargetElement_Tests
    {
        /// <summary>
        /// Create target with invalid name
        /// </summary>
        [Fact]
        public void AddTargetInvalidName()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.CreateTargetElement("@#$invalid@#$");
            });
        }
        /// <summary>
        /// Read targets in an empty project
        /// </summary>
        [Fact]
        public void ReadNoTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Assert.Empty(project.Targets);
        }

        /// <summary>
        /// Read an empty target
        /// </summary>
        [Fact]
        public void ReadEmptyTarget()
        {
            string content = @"
                    <Project>
                        <Target Name='t'/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);

            Assert.Equal(0, Helpers.Count(target.Children));
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Read attributes on the target element
        /// </summary>
        [Fact]
        public void ReadParameters()
        {
            ProjectTargetElement target = GetTargetXml();

            Assert.Equal("i", target.Inputs);
            Assert.Equal("o", target.Outputs);
            Assert.Equal("d", target.DependsOnTargets);
            Assert.Equal("c", target.Condition);
        }

        /// <summary>
        /// Set attributes on the target element
        /// </summary>
        [Fact]
        public void SetParameters()
        {
            ProjectTargetElement target = GetTargetXml();

            target.Inputs = "ib";
            target.Outputs = "ob";
            target.DependsOnTargets = "db";
            target.Condition = "cb";

            Assert.Equal("ib", target.Inputs);
            Assert.Equal("ob", target.Outputs);
            Assert.Equal("db", target.DependsOnTargets);
            Assert.Equal("cb", target.Condition);
        }

        /// <summary>
        /// Set null inputs on the target element
        /// </summary>
        [Fact]
        public void SetInvalidNullInputs()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.Inputs = null;
            });
        }
        /// <summary>
        /// Set null outputs on the target element
        /// </summary>
        [Fact]
        public void SetInvalidNullOutputs()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.Outputs = null;
            });
        }
        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [Fact]
        public void SetInvalidNullDependsOnTargets()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.DependsOnTargets = null;
            });
        }
        /// <summary>
        /// Set null dependsOnTargets on the target element
        /// </summary>
        [Fact]
        public void SetInvalidNullKeepDuplicateOutputs()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectTargetElement target = GetTargetXml();
                target.KeepDuplicateOutputs = null;
            });
        }
        /// <summary>
        /// Set null condition on the target element
        /// </summary>
        [Fact]
        public void SetNullCondition()
        {
            ProjectTargetElement target = GetTargetXml();
            target.Condition = null;

            Assert.Equal(String.Empty, target.Condition);
        }

        /// <summary>
        /// Read a target with a missing name
        /// </summary>
        [Fact]
        public void ReadInvalidMissingName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read a target with an invalid attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target XX='YY'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an target with two task children
        /// </summary>
        [Fact]
        public void ReadTargetTwoTasks()
        {
            ProjectTargetElement target = GetTargetXml();

            var tasks = Helpers.MakeList(target.Children);

            Assert.Equal(2, tasks.Count);

            ProjectTaskElement task1 = (ProjectTaskElement)tasks[0];
            ProjectTaskElement task2 = (ProjectTaskElement)tasks[1];

            Assert.Equal("t1", task1.Name);
            Assert.Equal("t2", task2.Name);
        }

        /// <summary>
        /// Set name
        /// </summary>
        [Fact]
        public void SetName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Name = "t2";

            Assert.Equal("t2", target.Name);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set inputs
        /// </summary>
        [Fact]
        public void SetInputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Inputs = "in";

            Assert.Equal("in", target.Inputs);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set outputs
        /// </summary>
        [Fact]
        public void SetOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Outputs = "out";

            Assert.Equal("out", target.Outputs);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set dependsontargets
        /// </summary>
        [Fact]
        public void SetDependsOnTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.DependsOnTargets = "dot";

            Assert.Equal("dot", target.DependsOnTargets);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set condition
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Condition = "c";

            Assert.Equal("c", target.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set KeepDuplicateOutputs attribute
        /// </summary>
        [Fact]
        public void SetKeepDuplicateOutputs()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.KeepDuplicateOutputs = "true";

            Assert.Equal("true", target.KeepDuplicateOutputs);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set return value.  Verify that setting to the empty string and null are
        /// both allowed and have distinct behaviour.
        /// </summary>
        [Fact]
        public void SetReturns()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            target.Returns = "@(a)";

            Assert.Equal("@(a)", target.Returns);
            Assert.True(project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = String.Empty;

            Assert.Equal(String.Empty, target.Returns);
            Assert.True(project.HasUnsavedChanges);

            Helpers.ClearDirtyFlag(project);

            target.Returns = null;

            Assert.Null(target.Returns);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Parse invalid property under target
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReadInvalidPropertyUnderTarget(bool enableNewBehavior)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                ChangeWaves.ResetStateForTests();
                if (!enableNewBehavior)
                {
                    env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave17_6.ToString());
                    BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
                }

                string projectFile = @"
                    <Project>
                        <Target Name='t'>
                            <test>m</test>
                        </Target>
                    </Project>";
                TransientTestFile file = env.CreateFile("proj.csproj", projectFile);
                ProjectCollection collection = new ProjectCollection();
                var error = Assert.Throws<InvalidProjectFileException>(() =>
                {
                    collection.LoadProject(file.Path).Build().ShouldBeTrue();
                });

                error.ErrorCode.ShouldMatch("MSB4067");
                var expectedString = "<PropertyGroup>";
                if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_6))
                {
                    error.Message.ShouldMatch(expectedString);
                }
                else
                {
                    error.Message.ShouldNotMatch(expectedString);
                }
            }
        }

        /// <summary>
        /// Helper to get an empty ProjectTargetElement with various attributes and two tasks
        /// </summary>
        private static ProjectTargetElement GetTargetXml()
        {
            string content = @"
                    <Project>
                        <Target Name='t' Inputs='i' Outputs='o' DependsOnTargets='d' Condition='c'>
                            <t1/>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            return target;
        }
    }
}
