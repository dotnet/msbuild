//-----------------------------------------------------------------------
// <copyright file="ProjectOnErrorElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectOnErrorElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

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
        [TestMethod]
        public void ReadTargetOnlyContainingOnError()
        {
            ProjectOnErrorElement onError = GetOnError();

            Assert.AreEqual("t", onError.ExecuteTargetsAttribute);
            Assert.AreEqual("c", onError.Condition);
        }

        /// <summary>
        /// Read a target with two onerrors, and some tasks
        /// </summary>
        [TestMethod]
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

            Assert.AreEqual("1", onError1.ExecuteTargetsAttribute);
            Assert.AreEqual("2", onError2.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Read onerror with no executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadMissingExecuteTargets()
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

            Assert.AreEqual(String.Empty, onError.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Read onerror with empty executetargets attribute
        /// </summary>
        /// <remarks>
        /// This was accidentally allowed in 2.0/3.5 but it should be an error now.
        /// </remarks>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadEmptyExecuteTargets()
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

            Assert.AreEqual(String.Empty, onError.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Read onerror with invalid attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidUnexpectedAttribute()
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

        /// <summary>
        /// Read onerror with invalid child element
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidUnexpectedChild()
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

        /// <summary>
        /// Read onerror before task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBeforeTask()
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

        /// <summary>
        /// Read onerror before task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBeforePropertyGroup()
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

        /// <summary>
        /// Read onerror before task
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidBeforeItemGroup()
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

        /// <summary>
        /// Set ExecuteTargets
        /// </summary>
        [TestMethod]
        public void SetExecuteTargetsValid()
        {
            ProjectOnErrorElement onError = GetOnError();

            onError.ExecuteTargetsAttribute = "t2";

            Assert.AreEqual("t2", onError.ExecuteTargetsAttribute);
        }

        /// <summary>
        /// Set ExecuteTargets to null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidExecuteTargetsNull()
        {
            ProjectOnErrorElement onError = GetOnError();

            onError.ExecuteTargetsAttribute = null;
        }

        /// <summary>
        /// Set ExecuteTargets to empty string
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetInvalidExecuteTargetsEmpty()
        {
            ProjectOnErrorElement onError = GetOnError();

            onError.ExecuteTargetsAttribute = String.Empty;
        }

        /// <summary>
        /// Set on error condition
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.Condition = "c";

            Assert.AreEqual("c", onError.Condition);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set on error executetargets value
        /// </summary>
        [TestMethod]
        public void SetExecuteTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            ProjectOnErrorElement onError = project.CreateOnErrorElement("et");
            target.AppendChild(onError);
            Helpers.ClearDirtyFlag(project);

            onError.ExecuteTargetsAttribute = "et2";

            Assert.AreEqual("et2", onError.ExecuteTargetsAttribute);
            Assert.AreEqual(true, project.HasUnsavedChanges);
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
