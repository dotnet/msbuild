// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_XML_SCHEMA_VALIDATION
using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Xml;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ProjectSchemaValidationHandlerTest
    {
        /***********************************************************************
         * 
         * Test:        ProjectSchemaValidationHandlerTest.VerifyProjectSchema
         *  
         * This calls VerifyProjectSchema to validate a project file passed, where
         * the project contents are invalid
         * 
         **********************************************************************/
        [Fact]
        public void VerifyInvalidProjectSchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] { };
            string projectFilename = null;
            string oldValueForMSBuildOldOM = null;
            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();

                projectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <MyInvalidProperty/>
                        </PropertyGroup>
                        <Target Name=`Build` />
                    </Project>
                    ");
                string quotedProjectFilename = "\"" + projectFilename + "\"";

                Assert.Equal(MSBuildApp.ExitType.InitializationError, MSBuildApp.Execute(@"c:\foo\msbuild.exe " + quotedProjectFilename + " /validate:\"" + msbuildTempXsdFilenames[0] + "\""));
            }
            finally
            {
                if (projectFilename != null) File.Delete(projectFilename);
                CleanupSchemaFiles(msbuildTempXsdFilenames);
                Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
            }
        }

        /// <summary>
        /// Checks that an exception is thrown when the schema being validated
        /// against is itself invalid
        /// </summary>
        [Fact]
        public void VerifyInvalidSchemaItself1
            (
            )
        {
            string invalidSchemaFile = null;
            string projectFilename = null;
            string oldValueForMSBuildOldOM = null;
            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                // Create schema files in the temp folder
                invalidSchemaFile = FileUtilities.GetTemporaryFile();

                File.WriteAllText(invalidSchemaFile, "<this_is_invalid_schema_content/>");

                projectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ");
                string quotedProjectFile = "\"" + projectFilename + "\"";

                Assert.Equal(MSBuildApp.ExitType.InitializationError, MSBuildApp.Execute(@"c:\foo\msbuild.exe " + quotedProjectFile + " /validate:\"" + invalidSchemaFile + "\""));
            }
            finally
            {
                if (projectFilename != null) File.Delete(projectFilename);
                if (invalidSchemaFile != null) File.Delete(invalidSchemaFile);
                Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
            }
        }

        /// <summary>
        /// Checks that an exception is thrown when the schema being validated
        /// against is itself invalid
        /// </summary>
        [Fact]
        public void VerifyInvalidSchemaItself2
            (
            )
        {
            string invalidSchemaFile = null;
            string projectFilename = null;
            string oldValueForMSBuildOldOM = null;

            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                // Create schema files in the temp folder
                invalidSchemaFile = FileUtilities.GetTemporaryFile();

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

                projectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ");

                string quotedProjectFile = "\"" + projectFilename + "\"";

                Assert.Equal(MSBuildApp.ExitType.InitializationError, MSBuildApp.Execute(@"c:\foo\msbuild.exe " + quotedProjectFile + " /validate:\"" + invalidSchemaFile + "\""));
            }
            finally
            {
                if (invalidSchemaFile != null) File.Delete(invalidSchemaFile);
                if (projectFilename != null) File.Delete(projectFilename);
                Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
            }
        }

        /***********************************************************************
         * 
         * Test:        ProjectSchemaValidationHandlerTest.VerifyProjectSchema
         *  
         * This calls VerifyProjectSchema to validate a project XML
         * specified in a string, where the project passed is valid
         * 
         **********************************************************************/
        [Fact]
        public void VerifyValidProjectSchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] { };
            string projectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <Target Name=`Build` />
                    </Project>
                    ");
            string oldValueForMSBuildOldOM = null;

            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();
                string quotedProjectFile = "\"" + projectFilename + "\"";

                Assert.Equal(MSBuildApp.ExitType.Success, MSBuildApp.Execute(@"c:\foo\msbuild.exe " + quotedProjectFile + " /validate:\"" + msbuildTempXsdFilenames[0] + "\""));

                //ProjectSchemaValidationHandler.VerifyProjectSchema
                //    (
                //    projectFilename, 
                //    msbuildTempXsdFilenames[0],
                //    @"c:\"
                //    );
            }
            finally
            {
                File.Delete(projectFilename);
                CleanupSchemaFiles(msbuildTempXsdFilenames);
                Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
            }
        }

        /// <summary>
        /// The test has a valid project file, importing an invalid project file.
        /// We should not validate imported files against the schema in V1, so this
        /// should not be caught by the schema
        /// </summary>
        [Fact]
        public void VerifyInvalidImportNotCaughtBySchema
            (
            )
        {
            string[] msbuildTempXsdFilenames = new string[] { };

            string importedProjectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup><UnknownProperty/></PropertyGroup>
                        <Target Name=`Build` />
                    </Project>
                ");

            string projectFilename = CreateTempFileOnDisk(@"
                    <Project xmlns=`msbuildnamespace`>
                        <Import Project=`{0}` />
                    </Project>

                ", importedProjectFilename);
            string oldValueForMSBuildOldOM = null;

            try
            {
                oldValueForMSBuildOldOM = Environment.GetEnvironmentVariable("MSBuildOldOM");
                Environment.SetEnvironmentVariable("MSBuildOldOM", "");

                // Create schema files in the temp folder
                msbuildTempXsdFilenames = PrepareSchemaFiles();
                string quotedProjectFile = "\"" + projectFilename + "\"";

                Assert.Equal(MSBuildApp.ExitType.Success, MSBuildApp.Execute(@"c:\foo\msbuild.exe " + quotedProjectFile + " /validate:\"" + msbuildTempXsdFilenames[0] + "\""));

                //ProjectSchemaValidationHandler.VerifyProjectSchema
                //    (
                //    projectFilename,
                //    msbuildTempXsdFilenames[0],
                //    @"c:\"
                //    );
            }
            finally
            {
                CleanupSchemaFiles(msbuildTempXsdFilenames);
                File.Delete(projectFilename);
                File.Delete(importedProjectFilename);
                Environment.SetEnvironmentVariable("MSBuildOldOM", oldValueForMSBuildOldOM);
            }
        }

        #region Helper Functions

        /// <summary>
        /// MSBuild schemas are embedded as a resource into Microsoft.Build.Engine.UnitTests.dll.
        /// Extract the stream from the resource and write the XSDs out to a temporary file,
        /// so that our schema validator can access it.
        /// </summary>
        private string[] PrepareSchemaFiles()
        {
            Stream msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.CommandLine.UnitTests.Microsoft.Build.xsd");
            StreamReader msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            string msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildTempXsdFilename = FileUtilities.GetTemporaryFile();
            File.WriteAllText(msbuildTempXsdFilename, msbuildXsdContents);

            msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.CommandLine.UnitTests.Microsoft.Build.Core.xsd");
            msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildXsdSubDirectory = Path.Combine(Path.GetTempPath(), "MSBuild");
            Directory.CreateDirectory(msbuildXsdSubDirectory);
            string msbuildTempXsdFilename2 = Path.Combine(msbuildXsdSubDirectory, "Microsoft.Build.Core.xsd");
            File.WriteAllText(msbuildTempXsdFilename2, msbuildXsdContents);

            msbuildXsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Build.CommandLine.UnitTests.Microsoft.Build.CommonTypes.xsd");
            msbuildXsdStreamReader = new StreamReader(msbuildXsdStream);
            msbuildXsdContents = msbuildXsdStreamReader.ReadToEnd();
            string msbuildTempXsdFilename3 = Path.Combine(msbuildXsdSubDirectory, "Microsoft.Build.CommonTypes.xsd");
            File.WriteAllText(msbuildTempXsdFilename3, msbuildXsdContents);

            return new string[] { msbuildTempXsdFilename, msbuildTempXsdFilename2, msbuildTempXsdFilename3 };
        }

        /// <summary>
        /// Gets rid of the temporary files created to hold the schemas for the duration
        /// of these unit tests.
        /// </summary>
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
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        FileUtilities.DeleteWithoutTrailingBackslash(msbuildXsdSubDirectory, true /* recursive */);
                        break;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000);
                        // Eat exceptions from the delete
                    }
                }
            }
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <remarks>Stolen from ObjectModelHelpers because we use relatively little
        /// of the ObjectModelHelpers functionality, so as to avoid having to include in
        /// this project everything that ObjectModelHelpers depends on</remarks>
        static internal string CreateTempFileOnDisk(string fileContents, params object[] args)
        {
            return CreateTempFileOnDiskNoFormat(String.Format(fileContents, args));
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <remarks>Stolen from ObjectModelHelpers because we use relatively little
        /// of the ObjectModelHelpers functionality, so as to avoid having to include in
        /// this project everything that ObjectModelHelpers depends on</remarks>
        static internal string CreateTempFileOnDiskNoFormat(string fileContents)
        {
            string projectFilePath = FileUtilities.GetTemporaryFile();

            File.WriteAllText(projectFilePath, CleanupFileContents(fileContents));

            return projectFilePath;
        }

        /// <summary>
        /// Does certain replacements in a string representing the project file contents.
        /// This makes it easier to write unit tests because the author doesn't have
        /// to worry about escaping double-quotes, etc.
        /// </summary>
        /// <remarks>Stolen from ObjectModelHelpers because we use relatively little
        /// of the ObjectModelHelpers functionality, so as to avoid having to include in
        /// this project everything that ObjectModelHelpers depends on</remarks>
        static private string CleanupFileContents(string projectFileContents)
        {
            // Replace reverse-single-quotes with double-quotes.
            projectFileContents = projectFileContents.Replace("`", "\"");

            // Place the correct MSBuild namespace into the <Project> tag.
            projectFileContents = projectFileContents.Replace("msbuildnamespace", msbuildNamespace);
            projectFileContents = projectFileContents.Replace("msbuilddefaulttoolsversion", msbuildDefaultToolsVersion);

            return projectFileContents;
        }

        private const string msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        private const string msbuildDefaultToolsVersion = "4.0";

        #endregion // Helper Functions
    }
}
#endif
