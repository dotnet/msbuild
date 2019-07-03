// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

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
        [Fact]
        public void ReadEmptyParameterGroup()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(s_contentEmptyParameterGroup);
            Assert.NotNull(parameterGroup);
            Assert.Equal(0, parameterGroup.Count);
            Assert.Null(parameterGroup.Parameters.GetEnumerator().Current);
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
            }
           );
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
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup BadAttribute='Hello'/>
                       </UsingTask>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                Assert.True(false);
            }
           );
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
