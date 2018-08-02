// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Framework;
using System.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using System.Xml;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    public class ElementLocationPublic_Tests
    {
        /// <summary>
        /// Check that we can get the file name off an element and attribute, even if 
        /// it wouldn't normally have got one because the project wasn't
        /// loaded from disk, or has been edited since.
        /// This is really a test of our XmlDocumentWithLocation.
        /// </summary>
        [Fact]
        public void ShouldHaveFilePathLocationEvenIfNotLoadedNorSavedYet()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.FullPath = "c:\\x";
            ProjectTargetElement target = project.CreateTargetElement("t");
            target.Outputs = "o";
            project.AppendChild(target);

            Assert.Equal(project.FullPath, target.Location.File);
            Assert.Equal(project.FullPath, target.OutputsLocation.File);
        }

        /// <summary>
        /// Element location should reflect rename.
        /// This is really a test of our XmlXXXXWithLocation.
        /// </summary>
        [Fact]
        public void XmlLocationReflectsRename()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.FullPath = "c:\\x";
            ProjectTargetElement target = project.CreateTargetElement("t");
            target.Outputs = "o";
            project.AppendChild(target);

            Assert.Equal(project.FullPath, target.Location.File);
            Assert.Equal(project.FullPath, target.OutputsLocation.File);

            project.FullPath = "c:\\y";

            Assert.Equal(project.FullPath, target.Location.File);
            Assert.Equal(project.FullPath, target.OutputsLocation.File);
        }

        /// <summary>
        /// We should cache ElementLocation objects for perf.
        /// </summary>
        [Fact]
        public void XmlLocationsAreCached()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.FullPath = "c:\\x";
            ProjectTargetElement target = project.CreateTargetElement("t");
            target.Outputs = "o";
            project.AppendChild(target);

            ElementLocation e1 = target.Location;
            ElementLocation e2 = target.OutputsLocation;

            Assert.Equal(true, Object.ReferenceEquals(e1, target.Location));
            Assert.Equal(true, Object.ReferenceEquals(e2, target.OutputsLocation));
        }

        /// <summary>
        /// Test many of the getters
        /// </summary>
        [Fact]
        public void LocationStringsMedley()
        {
            string content = @"
            <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
                    <UsingTask TaskName='t' AssemblyName='a' Condition='true'/>
                    <UsingTask TaskName='t' AssemblyFile='a' Condition='true'/>
                    <ItemDefinitionGroup Condition='true' Label='l'>
                        <m Condition='true'/>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <i Include='i' Condition='true' Exclude='r'>
                            <m Condition='true'/>
                        </i>
                    </ItemGroup>
                    <PropertyGroup>
                        <p Condition='true'/>
                    </PropertyGroup>
                    <Target Name='Build' Condition='true' Inputs='i' Outputs='o'>
                        <ItemGroup>
                            <i Include='i' Condition='true' Exclude='r'>
                                <m Condition='true'/>
                            </i>
                            <i Remove='r'/>
                        </ItemGroup>
                        <PropertyGroup>
                            <p Condition='true'/>
                        </PropertyGroup>
                        <Error Text='xyz' ContinueOnError='true' Importance='high'/>
                    </Target>
                    <Import Project='p' Condition='false'/>
                </Project>
                ";

            var project = ObjectModelHelpers.CreateInMemoryProject(content);

            string locations = project.Xml.Location.LocationString + "\r\n";

            foreach (var element in project.Xml.AllChildren)
            {
                foreach (var property in element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!property.Name.Equals("ImplicitImportLocation") && property.Name.Contains("Location"))
                    {
                        if (property.Name == "ParameterLocations")
                        {
                            var values = new List<KeyValuePair<string, ElementLocation>>(((ICollection<KeyValuePair<string, ElementLocation>>)property.GetValue(element, null)));

                            values.ForEach((value) => locations += value.Key + ":" + value.Value.LocationString + "\r\n");
                        }
                        else
                        {
                            var value = ((ElementLocation)property.GetValue(element, null));

                            if (value != null) // null means attribute is not present
                            {
                                locations += value.LocationString + "\r\n";
                            }
                        }
                    }
                }
            }

            locations = locations.Replace(project.FullPath, "c:\\foo\\bar.csproj");

            string expected = @"c:\foo\bar.csproj (2,13)
c:\foo\bar.csproj (3,32)
c:\foo\bar.csproj (3,45)
c:\foo\bar.csproj (3,62)
c:\foo\bar.csproj (3,21)
c:\foo\bar.csproj (4,32)
c:\foo\bar.csproj (4,45)
c:\foo\bar.csproj (4,62)
c:\foo\bar.csproj (4,21)
c:\foo\bar.csproj (5,42)
c:\foo\bar.csproj (5,59)
c:\foo\bar.csproj (5,21)
c:\foo\bar.csproj (6,28)
c:\foo\bar.csproj (6,25)
c:\foo\bar.csproj (8,21)
c:\foo\bar.csproj (9,28)
c:\foo\bar.csproj (9,57)
c:\foo\bar.csproj (9,40)
c:\foo\bar.csproj (9,25)
c:\foo\bar.csproj (10,32)
c:\foo\bar.csproj (10,29)
c:\foo\bar.csproj (13,21)
c:\foo\bar.csproj (14,28)
c:\foo\bar.csproj (14,25)
c:\foo\bar.csproj (16,29)
c:\foo\bar.csproj (16,59)
c:\foo\bar.csproj (16,70)
c:\foo\bar.csproj (16,29)
c:\foo\bar.csproj (16,42)
c:\foo\bar.csproj (16,21)
c:\foo\bar.csproj (17,25)
c:\foo\bar.csproj (18,32)
c:\foo\bar.csproj (18,61)
c:\foo\bar.csproj (18,44)
c:\foo\bar.csproj (18,29)
c:\foo\bar.csproj (19,36)
c:\foo\bar.csproj (19,33)
c:\foo\bar.csproj (21,32)
c:\foo\bar.csproj (21,29)
c:\foo\bar.csproj (23,25)
c:\foo\bar.csproj (24,32)
c:\foo\bar.csproj (24,29)
Text: (26,32)
Importance: (26,66)
c:\foo\bar.csproj (26,43)
c:\foo\bar.csproj (26,25)
c:\foo\bar.csproj (28,29)
c:\foo\bar.csproj (28,41)
c:\foo\bar.csproj (28,21)
";

            Helpers.VerifyAssertLineByLine(expected, locations);
        }
    }
}