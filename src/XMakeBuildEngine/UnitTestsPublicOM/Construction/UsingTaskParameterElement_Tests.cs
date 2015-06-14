// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

using NUnit.Framework;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectUsingParameterElement class
    /// </summary>
    [TestFixture]
    public class UsingTaskParameterElement_Tests
    {
        /// <summary>
        /// Parameter element with all attributes set
        /// </summary>
        private static string s_contentAllAttributesSet = @"
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
        private static string s_contentNoAttributesSet = @"
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
        [Test]
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
        [Test]
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
        [Test]
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
        [Test]
        public void SetType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = "newType";
            Assert.AreEqual("newType", parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set output value
        /// </summary>
        [Test]
        public void SetOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = "output";
            Assert.AreEqual("output", parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set required value
        /// </summary>
        [Test]
        public void SetRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = "required";
            Assert.AreEqual("required", parameter.Required);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type type to empty
        /// </summary>
        [Test]
        public void SetEmptyType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = String.Empty;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to empty
        /// </summary>
        [Test]
        public void SetEmptyOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to empty
        /// </summary>
        [Test]
        public void SetEmptyRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Required = String.Empty;
            Assert.AreEqual(bool.FalseString, parameter.Required);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type type to null
        /// </summary>
        [Test]
        public void SetNullType()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.ParameterType = null;
            Assert.AreEqual(typeof(String).FullName, parameter.ParameterType);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type output to null
        /// </summary>
        [Test]
        public void SetNullOutput()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
            Helpers.ClearDirtyFlag(parameter.ContainingProject);

            parameter.Output = null;
            Assert.AreEqual(bool.FalseString, parameter.Output);
            Assert.AreEqual(true, parameter.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Set type required to null
        /// </summary>
        [Test]
        public void SetNullRequired()
        {
            ProjectUsingTaskParameterElement parameter = GetParameterXml(s_contentAllAttributesSet);
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
