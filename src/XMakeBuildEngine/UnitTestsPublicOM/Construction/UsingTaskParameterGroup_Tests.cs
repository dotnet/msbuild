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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

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
        private static string contentEmptyParameterGroup = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <UsingTask TaskName='SuperTask' AssemblyFile='af' TaskFactory='AssemblyFactory'>
                           <ParameterGroup/>
                       </UsingTask>
                    </Project>
                ";

        /// <summary>
        /// ParameterGroup with duplicate child parameters
        /// </summary>
        private static string contentDuplicateParameters = @"
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
        private static string contentMultipleParameters = @"
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
        [TestMethod]
        public void ReadEmptyParameterGroup()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(contentEmptyParameterGroup);
            Assert.IsNotNull(parameterGroup);
            Assert.AreEqual(0, parameterGroup.Count);
            Assert.IsNull(parameterGroup.Parameters.GetEnumerator().Current);
        }

        /// <summary>
        /// Read simple parameterGroup body
        /// </summary>
        [TestMethod]
        public void ReadMutipleParameters()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(contentMultipleParameters);
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
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadDuplicateChildParameters()
        {
            UsingTaskParameterGroupElement parameterGroup = GetParameterGroupXml(contentDuplicateParameters);
            Assert.Fail();
        }
  
        /// <summary>
        /// Read parameterGroup with a attribute
        /// </summary>
        [TestMethod]
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
