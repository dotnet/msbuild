// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectOnErrorElement class
    /// </summary>
    [TestClass]
    public class ProjectOnErrorElement_Tests
    {
        /// <summary>
        /// Read a target containing only OnError
        /// </summary>
        [MSBuildTestMethod]
        public void ReadTargetOnlyContainingOnError()
        {
            ProjectOnErrorElement onError = GetOnError();

            Assert.AreEqual("t", onError.ExecuteTargetsAttribute);
            Assert.AreEqual("c", onError.Condition);
        }

        /// <summary>
        /// Read a target with two onerrors, and some tasks
        /// </summary>
        [MSBuildTestMethod]
        public void ReadTargetTwoOnErrors()
        {
            string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1/>
                            <t2/>
                            <OnError ExecuteTargets='1'/>
                            <OnError ExecuteTargets='2'/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            var onErrors = Helpers.MakeList(target.OnErrors);

            ProjectOnErrorElement onError1 = onErrors[0];
            ProjectOnErrorElement onError2 = onErrors[1];

            Assert.AreEqual("1", onError1.ExecuteTargetsAttribute);
            Assert.AreEqual("2", onError2.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Read onerror with no executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [MSBuildTestMethod]
        public void ReadMissingExecuteTargets()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
                ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);

                Assert.AreEqual(String.Empty, onError.ExecuteTargetsAttribute);
            });
        }
        /// <summary>
        /// Read onerror with empty executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [MSBuildTestMethod]
        public void ReadEmptyExecuteTargets()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets=''/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
                ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);

                Assert.AreEqual(String.Empty, onError.ExecuteTargetsAttribute);
            });
        }
        /// <summary>
        /// Read onerror with invalid attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidUnexpectedAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t' XX='YY'/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read onerror with invalid child element
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidUnexpectedChild()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'>
                                <X/>
                            </OnError>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBeforeTask()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <t/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBeforePropertyGroup()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <PropertyGroup/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read onerror before task
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBeforeItemGroup()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t'/>
                            <ItemGroup/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Set ExecuteTargets
        /// </summary>
        [MSBuildTestMethod]
        public void SetExecuteTargetsValid()
        {
            ProjectOnErrorElement onError = GetOnError();

            onError.ExecuteTargetsAttribute = "t2";

            Assert.AreEqual("t2", onError.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Set ExecuteTargets to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidExecuteTargetsNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectOnErrorElement onError = GetOnError();

                onError.ExecuteTargetsAttribute = null;
            });
        }
        /// <summary>
        /// Set ExecuteTargets to empty string
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidExecuteTargetsEmpty()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectOnErrorElement onError = GetOnError();

                onError.ExecuteTargetsAttribute = String.Empty;
            });
        }
        /// <summary>
        /// Set on error condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.Condition = "c";

            Assert.AreEqual("c", onError.Condition);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set on error executetargets value
        /// </summary>
        [MSBuildTestMethod]
        public void SetExecuteTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.ExecuteTargetsAttribute = "et2";

            Assert.AreEqual("et2", onError.ExecuteTargetsAttribute);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Get a basic ProjectOnErrorElement
        /// </summary>
        private static ProjectOnErrorElement GetOnError()
        {
            string content = @"
                    <Project>
                        <Target Name='t'>
                            <OnError ExecuteTargets='t' Condition='c'/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectOnErrorElement onError = (ProjectOnErrorElement)Helpers.GetFirst(target.Children);
            return onError;
        }
    }
}
