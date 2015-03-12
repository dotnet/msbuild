//-----------------------------------------------------------------------
// <copyright file="ProjectChooseElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectChooseElement class (and for ProjectWhenElement and ProjectOtherwiseElement).</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectChooseElement class (and for ProjectWhenElement and ProjectOtherwiseElement)
    /// </summary>
    [TestClass]
    public class ProjectChooseElement_Tests
    {
        /// <summary>
        /// Read choose with unexpected attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose X='Y'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with unexpected Condition attribute.
        /// Condition is not currently allowed on Choose.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidConditionAttribute()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose Condition='true'/>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with unexpected child
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidChild()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <X/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with a When containing no Condition attribute
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidWhen()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <When>
                                <PropertyGroup><x/></PropertyGroup>
                            </When>
                            <Otherwise>
                                <PropertyGroup><y/></PropertyGroup>
                            </Otherwise>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with only an otherwise
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOnlyOtherwise()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <Otherwise/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with two otherwises
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidTwoOtherwise()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <Otherwise/>
                            <Otherwise/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read choose with otherwise before when
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidOtherwiseBeforeWhen()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <Otherwise/>
                            <When Condition='c'/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read empty choose
        /// </summary>
        /// <remarks>
        /// One might think this should work but 2.0 required at least one When.
        /// </remarks>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyChoose()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectChooseElement choose = (ProjectChooseElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(null, Helpers.GetFirst(choose.Children));
        }

        /// <summary>
        /// Read choose with only a when
        /// </summary>
        [TestMethod]
        public void ReadChooseOnlyWhen()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <When Condition='c'/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectChooseElement choose = (ProjectChooseElement)Helpers.GetFirst(project.Children);

            Assert.AreEqual(1, Helpers.Count(choose.WhenElements));
            Assert.AreEqual(null, choose.OtherwiseElement);
        }

        /// <summary>
        /// Read basic choose
        /// </summary>
        [TestMethod]
        public void ReadChooseBothWhenOtherwise()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <When Condition='c1'/>
                            <When Condition='c2'/>
                            <Otherwise/>
                        </Choose>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectChooseElement choose = (ProjectChooseElement)Helpers.GetFirst(project.Children);

            List<ProjectWhenElement> whens = Helpers.MakeList(choose.WhenElements);
            Assert.AreEqual(2, whens.Count);
            Assert.AreEqual("c1", whens[0].Condition);
            Assert.AreEqual("c2", whens[1].Condition);
            Assert.IsNotNull(choose.OtherwiseElement);
        }

        /// <summary>
        /// Test stack overflow is prevented.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExcessivelyNestedChoose()
        {
            StringBuilder builder1 = new StringBuilder();
            StringBuilder builder2 = new StringBuilder();

            for (int i = 0; i < 52; i++)
            {
                builder1.Append("<Choose><When Condition='true'>");
                builder2.Append("</When></Choose>");
            }

            string content = "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>";
            content += builder1.ToString();
            content += builder2.ToString();
            content += @"</Project>";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Setting a When's condition should dirty the project
        /// </summary>
        [TestMethod]
        public void SettingWhenConditionDirties()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose>
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                            </When>      
                        </Choose>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));
            ProjectChooseElement choose = Helpers.GetFirst(project.Xml.ChooseElements);
            ProjectWhenElement when = Helpers.GetFirst(choose.WhenElements);
            when.Condition = "false";

            Assert.AreEqual("v1", project.GetPropertyValue("p"));

            project.ReevaluateIfNecessary();

            Assert.AreEqual(String.Empty, project.GetPropertyValue("p"));
        }
    }
}
