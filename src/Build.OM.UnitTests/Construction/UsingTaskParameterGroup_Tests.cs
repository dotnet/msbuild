// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectUsingParameterElement class
    /// </summary>
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
        [Fact]
        public void ReadEmptyParameterGroup()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentEmptyParameterGroup);
            Assert.NotNull(parameterGroup);
            Assert.Equal(0, parameterGroup.Count);
            Assert.Empty(parameterGroup.Parameters);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [Fact]
        public void ReadMutipleParameters()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentMultipleParameters);
            Assert.NotNull(parameterGroup);
            Assert.Equal(2, parameterGroup.Count);
            Assert.NotNull(parameterGroup.Parameters);

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

            Assert.True(foundFirst);
            Assert.True(foundSecond);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [Fact]
        public void ReadDuplicateChildParameters()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                GetParameterGroupXml(s_contentDuplicateParameters);
                Assert.True(false);
            });
        }
        /// <summary>
        /// Read parameterGroup with a attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup BadAttribute='Hello'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            });
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
