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
    /// Test the ProjectOutputElement class
    /// </summary>
    public class ProjectOutputElement_Tests
    {
        /// <summary>
        /// Read an output item
        /// </summary>
        [Fact]
        public void ReadOutputItem()
        {
            ProjectOutputElement output = GetOutputItem();

            Assert.Equal(false, output.IsOutputProperty);
            Assert.Equal(true, output.IsOutputItem);
            Assert.Equal("p", output.TaskParameter);
            Assert.Equal(String.Empty, output.PropertyName);
            Assert.Equal("i1", output.ItemType);
        }

        /// <summary>
        /// Read an output property
        /// </summary>
        [Fact]
        public void ReadOutputProperty()
        {
            ProjectOutputElement output = GetOutputProperty();

            Assert.Equal(true, output.IsOutputProperty);
            Assert.Equal(false, output.IsOutputItem);
            Assert.Equal("p", output.TaskParameter);
            Assert.Equal("p1", output.PropertyName);
            Assert.Equal(String.Empty, output.ItemType);
        }

        /// <summary>
        /// Read an output property with missing itemname and propertyname
        /// </summary>
        [Fact]
        public void ReadInvalidOutputWithoutPropertyOrItem()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with reserved property name
        /// </summary>
        [Fact]
        public void ReadInvalidReservedOutputPropertyName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [Fact]
        public void ReadInvalidOutputWithoutTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with missing taskparameter
        /// </summary>
        [Fact]
        public void ReadInvalidOutputWithEmptyTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with child element
        /// </summary>
        [Fact]
        public void ReadInvalidOutputWithChildElement()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with propertyname but an empty itemname attribute
        /// </summary>
        [Fact]
        public void ReadInvalidPropertyValueItemBlank()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read an output property with an itemname but an empty propertyname attribute
        /// </summary>
        [Fact]
        public void ReadInvalidItemValuePropertyBlank()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Modify the condition
        /// </summary>
        [Fact]
        public void SetOutputPropertyCondition()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.Condition = "c";
            Assert.Equal("c", output.Condition);
            Assert.Equal(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Modify the property name value
        /// </summary>
        [Fact]
        public void SetOutputPropertyName()
        {
            ProjectOutputElement output = GetOutputProperty();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.PropertyName = "p1b";
            Assert.Equal("p1b", output.PropertyName);
            Assert.Equal(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the item name value when property name is set
        /// </summary>
        [Fact]
        public void SetOutputPropertyItemType()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectOutputElement output = GetOutputProperty();

                output.ItemType = "i1b";
            }
           );
        }
        /// <summary>
        /// Set the item name value
        /// </summary>
        [Fact]
        public void SetOutputItemItemType()
        {
            ProjectOutputElement output = GetOutputItem();
            Helpers.ClearDirtyFlag(output.ContainingProject);

            output.ItemType = "p1b";
            Assert.Equal("p1b", output.ItemType);
            Assert.Equal(true, output.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Attempt to set the property name when the item name is set
        /// </summary>
        [Fact]
        public void SetOutputItemPropertyName()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectOutputElement output = GetOutputItem();

                output.PropertyName = "p1b";
            }
           );
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
