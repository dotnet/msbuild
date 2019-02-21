// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectChooseElement class (and for ProjectWhenElement and ProjectOtherwiseElement)
    /// </summary>
    public class ProjectChooseElement_Tests
    {
        /// <summary>
        /// Read choose with unexpected attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read choose with unexpected Condition attribute.
        /// Condition is not currently allowed on Choose.
        /// </summary>
        [Fact]
        public void ReadInvalidConditionAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose Condition='true'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read choose with unexpected child
        /// </summary>
        [Fact]
        public void ReadInvalidChild()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read choose with a When containing no Condition attribute
        /// </summary>
        [Fact]
        public void ReadInvalidWhen()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read choose with only an otherwise
        /// </summary>
        [Fact]
        public void ReadInvalidOnlyOtherwise()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read choose with two otherwises
        /// </summary>
        [Fact]
        public void ReadInvalidTwoOtherwise()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read choose with otherwise before when
        /// </summary>
        [Fact]
        public void ReadInvalidOtherwiseBeforeWhen()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read empty choose
        /// </summary>
        /// <remarks>
        /// One might think this should work but 2.0 required at least one When.
        /// </remarks>
        [Fact]
        public void ReadInvalidEmptyChoose()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Choose/>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
                ProjectChooseElement choose = (ProjectChooseElement)Helpers.GetFirst(project.Children);

                Assert.Null(Helpers.GetFirst(choose.Children));
            }
           );
        }
        /// <summary>
        /// Read choose with only a when
        /// </summary>
        [Fact]
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

            Assert.Equal(1, Helpers.Count(choose.WhenElements));
            Assert.Null(choose.OtherwiseElement);
        }

        /// <summary>
        /// Read basic choose
        /// </summary>
        [Fact]
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
            Assert.Equal(2, whens.Count);
            Assert.Equal("c1", whens[0].Condition);
            Assert.Equal("c2", whens[1].Condition);
            Assert.NotNull(choose.OtherwiseElement);
        }

        /// <summary>
        /// Test stack overflow is prevented.
        /// </summary>
        [Fact]
        public void ExcessivelyNestedChoose()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Setting a When's condition should dirty the project
        /// </summary>
        [Fact]
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

            Assert.Equal("v1", project.GetPropertyValue("p"));

            project.ReevaluateIfNecessary();

            Assert.Equal(String.Empty, project.GetPropertyValue("p"));
        }
    }
}
