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
    /// Tests for the ProjectUsingParameterElement class
    /// </summary>
    [TestClass]
    public class UsingTaskParameterElement_Tests
    {
        /// <summary>
        /// Parameter element with all attributes set
        /// </summary>
        private static string s_contentAllAttributesSet = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter ParameterType='System.String' Output='true' Required='false'/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// Parameter element with no attributes set
        /// </summary>
        private static string s_contentNoAttributesSet = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// Read simple task body
        /// </summary>
        [MSBuildTestMethod]
        public void ReadParameterWithAllAttributes()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);

            Assert.AreEqual("MyParameter", parameter.Name);
            Assert.AreEqual("System.String", parameter.ParameterType);
            Assert.AreEqual("true", parameter.Output);
            Assert.AreEqual("false", parameter.Required);
        }

        /// <summary>
        /// Read simple task body
        /// </summary>
        [MSBuildTestMethod]
        public void ReadParameterWithNOAttributes()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentNoAttributesSet);

            Assert.AreEqual("MyParameter", parameter.Name);
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(bool.FalseString, parameter.Required);
        }

        /// <summary>
        /// Read parameter with an invalid attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter Invaliid='System.String'/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Set type value
        /// </summary>
        [MSBuildTestMethod]
        public void SetType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = "newType";
            Assert.AreEqual("newType", parameter.ParameterType);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set output value
        /// </summary>
        [MSBuildTestMethod]
        public void SetOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = "output";
            Assert.AreEqual("output", parameter.Output);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set required value
        /// </summary>
        [MSBuildTestMethod]
        public void SetRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = "required";
            Assert.AreEqual("required", parameter.Required);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = String.Empty;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Required);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetNullType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = null;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetNullOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = null;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetNullRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = null;
            Assert.AreEqual(bool.FalseString, parameter.Required);
            Assert.IsTrue(parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to get a UsingTaskParameterElement from xml
        /// </summary>
        private static ProjectUsingTaskParameterElement GetParameterXml(string contents)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(contents);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            UsingTaskParameterGroupElement parameterGroup = usingTask.ParameterGroup;
            ProjectUsingTaskParameterElement body = Helpers.GetFirst(parameterGroup.Parameters);
            return body;
        }
    }
}
