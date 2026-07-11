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
    public class UsingTaskParameterGroup_Tests
    {
        /// <summary>
        /// ParameterGroup with no parameters inside
        /// </summary>
        private static string s_contentEmptyParameterGroup = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup/>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// ParameterGroup with duplicate child parameters
        /// </summary>
        private static string s_contentDuplicateParameters = @"
                    <Project>
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
                    <Project>
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
        [MSBuildTestMethod]
        public void ReadEmptyParameterGroup()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentEmptyParameterGroup);
            Assert.IsNotNull(parameterGroup);
            Assert.AreEqual(0, parameterGroup.Count);
            Assert.IsEmpty(parameterGroup.Parameters);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void ReadDuplicateChildParameters()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                GetParameterGroupXml(s_contentDuplicateParameters);
                Assert.Fail();
            });
        }
        /// <summary>
        /// Read parameterGroup with a attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup BadAttribute='Hello'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.Fail();
            });
        }
        /// <summary>
        /// Helper to get a UsingTaskParameterGroupElement from xml
        /// </summary>
        private static UsingTaskParameterGroupElement GetParameterGroupXml(string contents)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(contents);
            ProjectRootElement project = projectRootElementFromString.Project;

            ProjectUsingTaskElement usingTask = (ProjectUsingTaskElement)Helpers.GetFirst(project.Children);
            return usingTask.ParameterGroup;
        }
    }
}
