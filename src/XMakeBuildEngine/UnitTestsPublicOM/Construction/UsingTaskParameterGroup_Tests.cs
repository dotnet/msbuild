// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="UsingTaskParameterGroup_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the UsingTaskParameterGroupElement_Tests class.</summary>
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
    public class UsingTaskParameterGroup_Tests
    {
        /// <summary>
        /// ParameterGroup with no parameters inside
        /// </summary>
        private static string s_contentEmptyParameterGroup = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup/>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// ParameterGroup with duplicate child parameters
        /// </summary>
        private static string s_contentDuplicateParameters = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter/>
                              <MyParameter/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// ParameterGroup with multiple parameters
        /// </summary>
        private static string s_contentMultipleParameters = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup>
                              <MyParameter1 ParameterType='System.String' Output='true' Required='false'/>
                              <MyParameter2 ParameterType='System.String' Output='true' Required='false'/>
                           </ParameterGroup>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [Test]
        public void ReadEmptyParameterGroup()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentEmptyParameterGroup);
            Assert.IsNotNull(parameterGroup);
            Assert.AreEqual(0, parameterGroup.Count);
            Assert.IsNull(parameterGroup.Parameters.GetEnumerator().Current);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [Test]
        public void ReadMutipleParameters()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentMultipleParameters);
            Assert.IsNotNull(parameterGroup);
            Assert.AreEqual(2, parameterGroup.Count);
            Assert.IsNotNull(parameterGroup.Parameters);

            bool foundFirst = false;
            bool foundSecond = false;
            foreach (ProjectUsingTaskParameterElement parameter in parameterGroup.Parameters)
            {
                if (String.Equals("MyParameter1", parameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    foundFirst = true;
                }

                if (String.Equals("MyParameter2", parameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    foundSecond = true;
                }
            }

            Assert.IsTrue(foundFirst);
            Assert.IsTrue(foundSecond);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadDuplicateChildParameters()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentDuplicateParameters);
            Assert.Fail();
        }

        /// <summary>
        /// Read parameterGroup with a attribute
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup BadAttribute='Hello'/>
                       </UsingTask>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Assert.Fail();
        }

        /// <summary>
        /// Helper to get a UsingTaskParameterGroupElement from xml
        /// </summary>
        private static UsingTaskParameterGroupElement GetParameterGroupXml(string contents)
        {
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(contents)));
            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask.ParameterGroup;
        }
    }
}
