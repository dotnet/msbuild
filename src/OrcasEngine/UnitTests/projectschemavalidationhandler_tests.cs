// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ProjectSchemaValidationHandlerTest
    {
        /***********************************************************************
         * 
         * Test:        ProjectSchemaValidationHandlerTest.VerifyProjectSchema
         * Owner:       JomoF
         *  
         * This calls VerifyProjectSchema to validate a project XML
         * specified in a string
         * 
         **********************************************************************/
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void VerifyInvalidProjectSchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] {};
            try
            {
                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();

                string projectContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <MyInvalidTag/>
                        <Target Name=`Build` />
                    </Project>
                    ";

                Engine buildEngine = new Engine(@"c:\");
                ProjectSchemaValidationHandler validator = new ProjectSchemaValidationHandler(null, buildEngine.LoggingServices, @"c:\");

                try
                {
                    validator.VerifyProjectSchema(ObjectModelHelpers.CleanupFileContents(projectContents), 
                        msbuildTempXsdFilenames[0]);
                }
                catch (InvalidProjectFileException e)
                {
                    Assertion.AssertEquals(e.BaseMessage, ResourceUtilities.FormatResourceString("ProjectSchemaErrorHalt"));

                    throw;
                }
            }
            finally
            {
                CleanupSchemaFiles(msbuildTempXsdFilenames);
            }
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void VerifyInvalidSchemaItself1
            (
            )
        {
            string invalidSchemaFile = null;
            try
            {
                // Create schema files in the temp folder
                invalidSchemaFile = Path.GetTempFileName();

                File.WriteAllText(invalidSchemaFile, "<this_is_invalid_schema_content/>");
                
                string projectContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ";

                Engine buildEngine = new Engine(@"c:\");
                ProjectSchemaValidationHandler validator = new ProjectSchemaValidationHandler(null, buildEngine.LoggingServices, @"c:\");

                try
                {
                    validator.VerifyProjectSchema(ObjectModelHelpers.CleanupFileContents(projectContents), invalidSchemaFile);
                }
                catch (InvalidProjectFileException e)
                {
                    Console.WriteLine(e.Message);
                    Assertion.Assert(e.ErrorCode.Contains("MSB4070") || e.BaseMessage.Contains("MSB4070"));

                    throw;
                }
            }
            finally
            {
                if (invalidSchemaFile != null) File.Delete(invalidSchemaFile);
            }
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void VerifyInvalidSchemaItself2
            (
            )
        {
            string invalidSchemaFile = null;
            try
            {
                // Create schema files in the temp folder
                invalidSchemaFile = Path.GetTempFileName();

                File.WriteAllText(invalidSchemaFile, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xs:schema targetNamespace=""http://schemas.microsoft.com/developer/msbuild/2003"" xmlns:msb=""http://schemas.microsoft.com/developer/msbuild/2003"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" elementFormDefault=""qualified"">
    <xs:element name=""Project"">
        <xs:complexType>
            <xs:sequence>
                <xs:group ref=""x"" minOccurs=""0"" maxOccurs=""unbounded""/>
            </xs:sequence>
        </xs:complexType>
    </xs:element>
</xs:schema>

");

                string projectContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ";

                Engine buildEngine = new Engine(@"c:\");
                ProjectSchemaValidationHandler validator = new ProjectSchemaValidationHandler(null, buildEngine.LoggingServices, @"c:\");

                try
                {
                    validator.VerifyProjectSchema(ObjectModelHelpers.CleanupFileContents(projectContents), invalidSchemaFile);
                }
                catch (InvalidProjectFileException e)
                {
                    Console.WriteLine(e.Message);
                    Assertion.Assert(e.ErrorCode.Contains("MSB4070") || e.BaseMessage.Contains("MSB4070"));

                    throw;
                }
            }
            finally
            {
                if (invalidSchemaFile != null) File.Delete(invalidSchemaFile);
            }
        }

        /***********************************************************************
         * 
         * Test:        ProjectSchemaValidationHandlerTest.VerifyProjectSchema
         * Owner:       JomoF
         *  
         * This calls VerifyProjectSchema to validate a project XML
         * specified in a string
         * 
         **********************************************************************/
        [Test]
        public void VerifyValidProjectSchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] {};
            try
            {
                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();

                string projectContents = @"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ";

                Engine e = new Engine(@"c:\");
                ProjectSchemaValidationHandler validator = new ProjectSchemaValidationHandler(null, e.LoggingServices, @"c:\");

                validator.VerifyProjectSchema(ObjectModelHelpers.CleanupFileContents(projectContents), 
                    msbuildTempXsdFilenames[0]);
            }
            finally
            {
                CleanupSchemaFiles(msbuildTempXsdFilenames);
            }
        }

        /// <summary>
        /// The test has a valid project file, importing an invalid project file.
        /// We should not validate imported files against the schema in V1, so this
        /// should not be caught by the schema
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void VerifyInvalidImportNotCaughtBySchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] {};

            string importedProjectFilename = ObjectModelHelpers.CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup><UnknownProperty/></PropertyGroup>
                        <Target Name=`Build` />
                    </Project>
                ");

            string projectFilename = ObjectModelHelpers.CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <Import Project=`{0}` />
                    </Project>

                ", importedProjectFilename);

            try
            {
                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();

                Project p = new Project(new Engine(@"c:\"));
                p.IsValidated = true;
                p.SchemaFile = msbuildTempXsdFilenames[0];
                p.Load(projectFilename);
            }
            finally
            {
                CleanupSchemaFiles(msbuildTempXsdFilenames);
                File.Delete(projectFilename);
                File.Delete(importedProjectFilename);
            }
        }

        /// <summary>
        /// MSBuild schemas are embedded as a resource into Microsoft.Build.Engine.UnitTests.dll.
        /// Extract the stream from the resource and write the XSDs out to a temporary file,
        /// so that our schema validator can access it.
        /// </summary>
        /// <owner>danmose</owner>
        private string[] PrepareSchemaFiles()
        {
            Stream msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.Engine.Unittest.Microsoft.Build.xsd");
            StreamReader msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            string msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildTempXsdFilename = Path.GetTempFileName();
            File.WriteAllText(msbuildTempXsdFilename, msbuildXsdContents);
            msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.Engine.Unittest.Microsoft.Build.Core.xsd");
            msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildXsdSubDirectory = Path.Combine(Path.GetTempPath(), "MSBuild");
            Directory.CreateDirectory(msbuildXsdSubDirectory);
            string msbuildTempXsdFilename2 = Path.Combine(msbuildXsdSubDirectory, "Microsoft.Build.Core.xsd");
            File.WriteAllText(msbuildTempXsdFilename2, msbuildXsdContents);
            msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.Engine.Unittest.Microsoft.Build.CommonTypes.xsd");
            msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildTempXsdFilename3 = Path.Combine(msbuildXsdSubDirectory, "Microsoft.Build.CommonTypes.xsd");
            File.WriteAllText(msbuildTempXsdFilename3, msbuildXsdContents);
            return new string[] { msbuildTempXsdFilename, msbuildTempXsdFilename2, msbuildTempXsdFilename3 };
        }

        private void CleanupSchemaFiles(string[] msbuildTempXsdFilenames)
        {
            foreach (string file in msbuildTempXsdFilenames)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            string msbuildXsdSubDirectory = Path.Combine(Path.GetTempPath(), "MSBuild");
            if (Directory.Exists(msbuildXsdSubDirectory))
            {
                Directory.Delete(msbuildXsdSubDirectory);
            }
        }
    }
}
