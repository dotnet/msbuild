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
    /// Test the ProjectOutputElement class
    /// </summary>
    [TestClass]
    public class ProjectOutputElement_Tests
    {
        /// <summary>
        /// Read an output item
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOutputItem()
        {
            ProjectOutputElement output = GetOutputItem();

            Assert.IsFalse(output.IsOutputProperty);
            Assert.IsTrue(output.IsOutputItem);
            Assert.AreEqual("p", output.TaskParameter);
            Assert.AreEqual(String.Empty, output.PropertyName);
            Assert.AreEqual("i1", output.ItemType);
        }

        /// <summary>
        /// Read an output property
        /// </summary>
        [MSBuildTestMethod]
        public void ReadOutputProperty()
        {
            ProjectOutputElement output = GetOutputProperty();

            Assert.IsTrue(output.IsOutputProperty);
            Assert.IsFalse(output.IsOutputItem);
            Assert.AreEqual("p", output.TaskParameter);
            Assert.AreEqual("p1", output.PropertyName);
            Assert.AreEqual(String.Empty, output.ItemType);
        }

        /// <summary>
        /// Read an output property with missing itemname and propertyname
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidOutputWithoutPropertyOrItem()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with reserved property name
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidReservedOutputPropertyName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' PropertyName='MSBuildProjectFile'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidOutputWithoutTaskName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output ItemName='i'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidOutputWithEmptyTaskName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskName='' ItemName='i'/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with child element
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidOutputWithChildElement()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
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

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with propertyname but an empty itemname attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidPropertyValueItemBlank()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='t' PropertyName='p' ItemName=''/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read an output property with an itemname but an empty propertyname attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidItemValuePropertyBlank()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='t' ItemName='i' PropertyName=''/>
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Modify the condition
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputPropertyCondition()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.Condition = "c";
            Assert.AreEqual("c", output.Condition);
            Assert.IsTrue(output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Modify the property name value
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputPropertyName()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.PropertyName = "p1b";
            Assert.AreEqual("p1b", output.PropertyName);
            Assert.IsTrue(output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the item name value when property name is set
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputPropertyItemType()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectOutputElement output = GetOutputProperty();

                output.ItemType = "i1b";
            });
        }
        /// <summary>
        /// Set the item name value
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputItemItemType()
        {
            ProjectOutputElement output = GetOutputItem();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.ItemType = "p1b";
            Assert.AreEqual("p1b", output.ItemType);
            Assert.IsTrue(output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the property name when the item name is set
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutputItemPropertyName()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectOutputElement output = GetOutputItem();

                output.PropertyName = "p1b";
            });
        }
        /// <summary>
        /// Helper to get a ProjectOutputElement for an output item
        /// </summary>
        private static ProjectOutputElement GetOutputItem()
        {
            string content = @"
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' ItemName='i1' />
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
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
                    <Project>
                        <Target Name='t'>
                            <t1>
                                <Output TaskParameter='p' PropertyName='p1' />
                            </t1>
                            <t2/>
                        </Target>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectTaskElement task = (ProjectTaskElement)Helpers.GetFirst(target.Children);
            return Helpers.GetFirst(task.Outputs);
        }
    }
}
