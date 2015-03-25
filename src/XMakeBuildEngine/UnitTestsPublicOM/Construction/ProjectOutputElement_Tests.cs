// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ProjectOutputElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectOutputElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using NUnit.Framework;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectOutputElement class
    /// </summary>
    [TestFixture]
    public class ProjectOutputElement_Tests
    {
        /// <summary>
        /// Read an output item
        /// </summary>
        [Test]
        public void ReadOutputItem()
        {
            ProjectOutputElement output = GetOutputItem();

            Assert.AreEqual(false, output.IsOutputProperty);
            Assert.AreEqual(true, output.IsOutputItem);
            Assert.AreEqual("p", output.TaskParameter);
            Assert.AreEqual(String.Empty, output.PropertyName);
            Assert.AreEqual("i1", output.ItemType);
        }

        /// <summary>
        /// Read an output property
        /// </summary>
        [Test]
        public void ReadOutputProperty()
        {
            ProjectOutputElement output = GetOutputProperty();

            Assert.AreEqual(true, output.IsOutputProperty);
            Assert.AreEqual(false, output.IsOutputItem);
            Assert.AreEqual("p", output.TaskParameter);
            Assert.AreEqual("p1", output.PropertyName);
            Assert.AreEqual(String.Empty, output.ItemType);
        }

        /// <summary>
        /// Read an output property with missing itemname and propertyname
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOutputWithoutPropertyOrItem()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with reserved property name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidReservedOutputPropertyName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' PropertyName='MSBuildProjectFile'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOutputWithoutTaskName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output ItemName='i'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOutputWithEmptyTaskName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskName='' ItemName='i'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with child element
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOutputWithChildElement()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output ItemName='i' TaskParameter='x'>
                                     xxxxxxx
                                </Output>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with propertyname but an empty itemname attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidPropertyValueItemBlank()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='t' PropertyName='p' ItemName=''/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read an output property with an itemname but an empty propertyname attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidItemValuePropertyBlank()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='t' ItemName='i' PropertyName=''/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Modify the condition
        /// </summary>
        [Test]
        public void SetOutputPropertyCondition()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.Condition = "c";
            Assert.AreEqual("c", output.Condition);
            Assert.AreEqual(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Modify the property name value
        /// </summary>
        [Test]
        public void SetOutputPropertyName()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.PropertyName = "p1b";
            Assert.AreEqual("p1b", output.PropertyName);
            Assert.AreEqual(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the item name value when property name is set
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetOutputPropertyItemType()
        {
            ProjectOutputElement output = GetOutputProperty();

            output.ItemType = "i1b";
        }

        /// <summary>
        /// Set the item name value
        /// </summary>
        [Test]
        public void SetOutputItemItemType()
        {
            ProjectOutputElement output = GetOutputItem();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.ItemType = "p1b";
            Assert.AreEqual("p1b", output.ItemType);
            Assert.AreEqual(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the property name when the item name is set
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetOutputItemPropertyName()
        {
            ProjectOutputElement output = GetOutputItem();

            output.PropertyName = "p1b";
        }

        /// <summary>
        /// Helper to get a ProjectOutputElement for an output item
        /// </summary>
        private static ProjectOutputElement GetOutputItem()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' ItemName='i1' />
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectTaskElement task = (ProjectTaskElement)Helpers.GetFirst(target.Children);
            return Helpers.GetFirst(task.Outputs);
        }

        /// <summary>
        /// Helper to get a ProjectOutputElement for an output property
        /// </summary>
        private static ProjectOutputElement GetOutputProperty()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' PropertyName='p1' />
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectTaskElement task = (ProjectTaskElement)Helpers.GetFirst(target.Children);
            return Helpers.GetFirst(task.Outputs);
        }
    }
}
