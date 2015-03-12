//-----------------------------------------------------------------------
// <copyright file="UsingTaskParameterElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the UsingTaskParameterElement class.</summary>
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
    /// Tests for the ProjectUsingParameterElement class
    /// </summary>
    [TestClass]
    public class UsingTaskParameterElement_Tests
    {
        /// <summary>
        /// Parameter element with all attributes set
        /// </summary>
        private static string contentAllAttributesSet = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
        private static string contentNoAttributesSet = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
        [TestMethod]
        public void ReadParameterWithAllAttributes()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);

            Assert.AreEqual("MyParameter", parameter.Name);
            Assert.AreEqual("System.String", parameter.ParameterType);
            Assert.AreEqual("true", parameter.Output);
            Assert.AreEqual("false", parameter.Required);
        }

        /// <summary>
        /// Read simple task body
        /// </summary>
        [TestMethod]
        public void ReadParameterWithNOAttributes()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentNoAttributesSet);

            Assert.AreEqual("MyParameter", parameter.Name);
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(bool.FalseString, parameter.Required);
        }

        /// <summary>
        /// Read parameter with an invalid attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter Invaliid='System.String'/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Assert.Fail();
        }

        /// <summary>
        /// Set type value
        /// </summary>
        [TestMethod]
        public void SetType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = "newType";
            Assert.AreEqual("newType", parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set output value
        /// </summary>
        [TestMethod]
        public void SetOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = "output";
            Assert.AreEqual("output", parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set required value
        /// </summary>
        [TestMethod]
        public void SetRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = "required";
            Assert.AreEqual("required", parameter.Required);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type type to empty
        /// </summary>
        [TestMethod]
        public void SetEmptyType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = String.Empty;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to empty
        /// </summary>
        [TestMethod]
        public void SetEmptyOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to empty
        /// </summary>
        [TestMethod]
        public void SetEmptyRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Required);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type type to null
        /// </summary>
        [TestMethod]
        public void SetNullType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = null;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to null
        /// </summary>
        [TestMethod]
        public void SetNullOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = null;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to null
        /// </summary>
        [TestMethod]
        public void SetNullRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = null;
            Assert.AreEqual(bool.FalseString, parameter.Required);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Helper to get a UsingTaskParameterElement from xml
        /// </summary>
        private static ProjectUsingTaskParameterElement GetParameterXml(string contents)
        {
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(contents)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            UsingTaskParameterGroupElement parameterGroup = usingTask.ParameterGroup;
            ProjectUsingTaskParameterElement body = Helpers.GetFirst(parameterGroup.Parameters);
            return body;
        }
    }
}
