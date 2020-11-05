// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.Construction;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectOnErrorElement class
    /// </summary>
    public class ProjectOnErrorElement_Tests
    {
        /// <summary>
        /// Read a target containing only OnError
        /// </summary>
        [Fact]
        public void ReadTargetOnlyContainingOnError()
        {
            ProjectOnErrorElement onError = GetOnError();

            Assert.Equal("t", onError.ExecuteTargetsAttribute);
            Assert.Equal("c", onError.Condition);
        }

        /// <summary>
        /// Read a target with two onerrors, and some tasks
        /// </summary>
        [Fact]
        public void ReadTargetTwoOnErrors()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1/>
                            <t2/>
                            <OnError ExecuteTargets='1'/>
                            <OnError ExecuteTargets='2'/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            var onErrors = Helpers.MakeList(target.OnErrors);

            ProjectOnErrorElement onError1 = onErrors[0];
            ProjectOnErrorElement onError2 = onErrors[1];

            Assert.Equal("1", onError1.ExecuteTargetsAttribute);
            Assert.Equal("2", onError2.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Read onerror with no executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [Fact]
        public void ReadMissingExecuteTargets()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
                ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);

                Assert.Equal(String.Empty, onError.ExecuteTargetsAttribute);
            }
           );
        }
        /// <summary>
        /// Read onerror with empty executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [Fact]
        public void ReadEmptyExecuteTargets()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets=''/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
                ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);

                Assert.Equal(String.Empty, onError.ExecuteTargetsAttribute);
            }
           );
        }
        /// <summary>
        /// Read onerror with invalid attribute
        /// </summary>
        [Fact]
        public void ReadInvalidUnexpectedAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t' XX='YY'/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read onerror with invalid child element
        /// </summary>
        [Fact]
        public void ReadInvalidUnexpectedChild()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'>
                                <X/>
                            </OnError>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [Fact]
        public void ReadInvalidBeforeTask()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <t/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [Fact]
        public void ReadInvalidBeforePropertyGroup()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <PropertyGroup/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [Fact]
        public void ReadInvalidBeforeItemGroup()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <ItemGroup/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Set ExecuteTargets
        /// </summary>
        [Fact]
        public void SetExecuteTargetsValid()
        {
            ProjectOnErrorElement onError = GetOnError();

            onError.ExecuteTargetsAttribute = "t2";

            Assert.Equal("t2", onError.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Set ExecuteTargets to null
        /// </summary>
        [Fact]
        public void SetInvalidExecuteTargetsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectOnErrorElement onError = GetOnError();

                onError.ExecuteTargetsAttribute = null;
            }
           );
        }
        /// <summary>
        /// Set ExecuteTargets to empty string
        /// </summary>
        [Fact]
        public void SetInvalidExecuteTargetsEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectOnErrorElement onError = GetOnError();

                onError.ExecuteTargetsAttribute = String.Empty;
            }
           );
        }
        /// <summary>
        /// Set on error condition
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.Condition = "c";

            Assert.Equal("c", onError.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set on error executetargets value
        /// </summary>
        [Fact]
        public void SetExecuteTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.ExecuteTargetsAttribute = "et2";

            Assert.Equal("et2", onError.ExecuteTargetsAttribute);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Get a basic ProjectOnErrorElement
        /// </summary>
        private static ProjectOnErrorElement GetOnError()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <OnError ExecuteTargets='t' Condition='c'/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);
            return onError;
        }
    }
}
