// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

using Xunit;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Unit tests for Importing from $(MSBuildExtensionsPath*)
    /// </summary>
    public class ImportFromMSBuildExtensionsPathTests : IDisposable
    {
        string toolsVersionToUse = null;

        public ImportFromMSBuildExtensionsPathTests()
        {
            toolsVersionToUse = new ProjectCollection().DefaultToolsVersion;
        }

        public void Dispose()
        {
            ToolsetConfigurationReaderTestHelper.CleanUp();
        }

        [Fact]
        public void ImportFromExtensionsPathFound()
        {
            CreateAndBuildProjectForImportFromExtensionsPath("MSBuildExtensionsPath", (p, l) => Assert.True(p.Build()));
        }

        [Fact]
        public void ImportFromExtensionsPathNotFound()
        {
            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), GetExtensionTargetsFileContent1());
                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

                var projColln = GetProjectCollection();

                projColln.ResetToolsetsForTests(WriteConfigFileAndGetReader("MSBuildExtensionsPath", extnDir1, Path.Combine("tmp", "nonexistent")));
                var logger = new MockLogger();
                projColln.RegisterLogger(logger);

                Assert.Throws<InvalidProjectFileException>(() => projColln.LoadProject(mainProjectPath));

                logger.AssertLogContains("MSB4226");
            }
            finally
            {
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }
                if (extnDir1 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir1, recursive: true);
                }
            }
        }

        [Fact]
        public void ConditionalImportFromExtensionsPathNotFound()
        {
            string extnTargetsFileContentWithCondition = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FooBar</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$(MSBuildExtensionsPath)\bar\extn2.proj' Condition=""Exists('$(MSBuildExtensionsPath)\bar\extn2.proj')""/>
                </Project>
                ";

            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContentWithCondition);
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath", new string[] { extnDir1, Path.Combine("tmp", "nonexistent") },
                                                            null,
                                                            (p, l) => {
                                                                Assert.True(p.Build());

                                                                l.AssertLogContains("Running FromExtn");
                                                                l.AssertLogContains("PropertyFromExtn1: FooBar");
                                                            });
        }

        [Fact]
        public void ImportFromExtensionsPathCircularImportError()
        {
            string extnTargetsFileContent1 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$(MSBuildExtensionsPath)\foo\extn2.proj' />
                </Project>
                ";

            string extnTargetsFileContent2 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn2'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='{0}'/>
                </Project>
                ";

            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());
            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent1);
            string extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("foo", "extn2.proj"),
                                                            String.Format(extnTargetsFileContent2, mainProjectPath));

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath",
                                                        new string[] { extnDir2, Path.Combine("tmp", "nonexistent"), extnDir1 },
                                                        null,
                                                        (p, l) => l.AssertLogContains("MSB4210"));
        }

        [Fact]
        public void ExtensionPathFallbackIsCaseInsensitive()
        {
            string mainTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Main'>
                        <Message Text='Running Main'/>
                    </Target>

                    <Import Project='$(msbuildExtensionsPath)\foo\extn.proj'/>
                </Project>";

            string extnTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn'>
                        <Message Text='Running {0}'/>
                    </Target>
                </Project>
                ";

            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent);

            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", mainTargetsFileContent);

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath",
                new[] { extnDir1 },
                null,
                (project, logger) =>
                {
                    Console.WriteLine(logger.FullLog);
                    Console.WriteLine("checking FromExtn");
                    Assert.True(project.Build("FromExtn"));
                    Console.WriteLine("checking logcontains");
                    logger.AssertLogDoesntContain("MSB4057"); // Should not contain TargetDoesNotExist
                });
        }

        [Fact]
        public void ImportFromExtensionsPathWithWildCard()
        {
            string mainTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Main'>
                        <Message Text='Running Main'/>
                    </Target>

                    <Import Project='$(MSBuildExtensionsPath)\foo\*.proj'/>
                </Project>";

            string extnTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='{0}'>
                        <Message Text='Running {0}'/>
                    </Target>
                </Project>
                ";

            // Importing a wildcard will union all matching results from all fallback locations.
            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"),
                string.Format(extnTargetsFileContent, "FromExtn1"));
            string extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("foo", "extn.proj"),
                string.Format(extnTargetsFileContent, "FromExtn2"));

            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", mainTargetsFileContent);

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath",
                new[] { extnDir1, Path.Combine("tmp", "nonexistent"), extnDir2 },
                null,
                (project, logger) =>
                {
                    Console.WriteLine(logger.FullLog);
                    Console.WriteLine("checking FromExtn1");
                    Assert.True(project.Build("FromExtn1"));
                    Console.WriteLine("checking FromExtn2");
                    Assert.True(project.Build("FromExtn2"));
                    Console.WriteLine("checking logcontains");
                    logger.AssertLogDoesntContain("MSB4057"); // Should not contain TargetDoesNotExist
                });
        }

        [Fact]
        public void ImportFromExtensionsPathWithWildCardAndSelfImport()
        {
            string mainTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Main'>
                        <Message Text='Running Main'/>
                    </Target>

                    <Import Project='$(MSBuildExtensionsPath)\circularwildcardtest\*.proj'/>
                </Project>";

            string extnTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='{0}'>
                        <Message Text='Running {0}'/>
                    </Target>
                </Project>";

            string extnTargetsFileContent2 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Import Project='$(MSBuildExtensionsPath)\circularwildcardtest\*.proj'/>
                    <Target Name='{0}'>
                        <Message Text='Running {0}'/>
                    </Target>
                </Project>";

            // Importing a wildcard will union all matching results from all fallback locations.
            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("circularwildcardtest", "extn.proj"),
                string.Format(extnTargetsFileContent, "FromExtn1"));
            string extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("circularwildcardtest", "extn.proj"),
                string.Format(extnTargetsFileContent, "FromExtn2"));
            string extnDir3 = GetNewExtensionsPathAndCreateFile("extensions3", Path.Combine("circularwildcardtest", "extn3.proj"),
                string.Format(extnTargetsFileContent2, "FromExtn3"));

            // Main project path is under "circularwildcardtest"
            // Note: This project will try to be imported again and cause a warning (MSB4210). This test should ensure that the
            // code does not stop looking in the fallback locations when this happens (extn3.proj should still be imported).
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory(Path.Combine("extensions2", "circularwildcardtest", "main.proj"), mainTargetsFileContent);

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath",
                new[] { extnDir1, extnDir2, extnDir3 },
                null,
                (project, logger) =>
                {
                    Console.WriteLine(logger.FullLog);
                    Assert.True(project.Build("FromExtn1"));
                    Assert.True(project.Build("FromExtn2"));
                    Assert.True(project.Build("FromExtn3"));
                    logger.AssertLogContains("MSB4210");
                });
        }

        [Fact]
        public void ImportFromExtensionsPathWithWildCardNothingFound()
        {
            string extnTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$(MSBuildExtensionsPath)\non-existant\*.proj'/>
                </Project>
                ";

            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent);
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath", new string[] { Path.Combine("tmp", "nonexistent"), extnDir1 },
                                                    null, (p, l) => Assert.True(p.Build()));
        }

        [Fact]
        public void ImportFromExtensionsPathInvalidFile()
        {
            string extnTargetsFileContent = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent);
                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

                var projColln = GetProjectCollection();
                projColln.ResetToolsetsForTests(WriteConfigFileAndGetReader("MSBuildExtensionsPath", extnDir1,
                                                                                Path.Combine("tmp", "nonexistent")));
                var logger = new MockLogger();
                projColln.RegisterLogger(logger);

                Assert.Throws<InvalidProjectFileException>(() => projColln.LoadProject(mainProjectPath));
                logger.AssertLogContains("MSB4024");
            }
            finally
            {
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }
                if (extnDir1 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir1, recursive: true);
                }
            }
        }

        [Fact]
        public void ImportFromExtensionsPathSearchOrder()
        {
            string extnTargetsFileContent1 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FromFirstFile</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                </Project>
                ";

            string extnTargetsFileContent2 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FromSecondFile</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                </Project>
                ";


            // File with the same name available in two different extension paths, but the one from the first
            // directory in MSBuildExtensionsPath environment variable should get loaded
            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent1);
            string extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("foo", "extn.proj"), extnTargetsFileContent2);
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

            CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, "MSBuildExtensionsPath", new string[] { extnDir2, Path.Combine("tmp", "nonexistent"), extnDir1 },
                                                            null,
                                                            (p, l) => {
                                                                Assert.True(p.Build());

                                                                l.AssertLogContains("Running FromExtn");
                                                                l.AssertLogContains("PropertyFromExtn1: FromSecondFile");
                                                            });
        }

        [Fact]
        public void ImportFromExtensionsPathSearchOrder2()
        {
            string extnTargetsFileContent1 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FromFirstFile</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                </Project>
                ";

            string extnTargetsFileContent2 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FromSecondFile</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                </Project>
                ";

            // File with the same name available in two different extension paths, but the one from the first
            // directory in MSBuildExtensionsPath environment variable should get loaded
            string extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"), extnTargetsFileContent1);
            string extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("foo", "extn.proj"), extnTargetsFileContent2);
            string mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

            // MSBuildExtensionsPath* property value has highest priority for the lookups
            try
            {
                var projColln = GetProjectCollection();
                projColln.ResetToolsetsForTests(WriteConfigFileAndGetReader("MSBuildExtensionsPath", Path.Combine("tmp", "non-existent"), extnDir1));
                var logger = new MockLogger();
                projColln.RegisterLogger(logger);
                var project = projColln.LoadProject(mainProjectPath);

                project.SetProperty("MSBuildExtensionsPath", extnDir2);
                project.ReevaluateIfNecessary();
                Assert.True(project.Build());

                logger.AssertLogContains("Running FromExtn");
                logger.AssertLogContains("PropertyFromExtn1: FromSecondFile");
            }
            finally
            {
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }
                if (extnDir1 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir1, recursive: true);
                }
                if (extnDir2 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir2, recursive: true);
                }
            }
        }

        [Fact]
        public void ImportOrderFromExtensionsPath32()
        {
            CreateAndBuildProjectForImportFromExtensionsPath("MSBuildExtensionsPath32", (p, l) => Assert.True(p.Build()));
        }

        [Fact]
        public void ImportOrderFromExtensionsPath64()
        {
            CreateAndBuildProjectForImportFromExtensionsPath("MSBuildExtensionsPath64", (p, l) => Assert.True(p.Build()));
        }

        // Use MSBuildExtensionsPath, MSBuildExtensionsPath32 and MSBuildExtensionsPath64 in the build
        [Fact]
        public void ImportFromExtensionsPathAnd32And64()
        {
            string extnTargetsFileContentTemplate = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn{0}' DependsOnTargets='{1}'>
                        <Message Text='Running FromExtn{0}'/>
                    </Target>
                    {2}
                </Project>
                ";

            var configFileContents = @"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""" + toolsVersionToUse + @""">
                     <toolset toolsVersion=""" + toolsVersionToUse + @""">
                       <property name=""MSBuildToolsPath"" value="".""/>
                       <property name=""MSBuildBinPath"" value=""" + /*v4Folder*/"." + @"""/>
                       <projectImportSearchPaths>
                         <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                           <property name=""MSBuildExtensionsPath"" value=""{0}"" />
                           <property name=""MSBuildExtensionsPath32"" value=""{1}"" />
                           <property name=""MSBuildExtensionsPath64"" value=""{2}"" />
                         </searchPaths>
                       </projectImportSearchPaths>
                      </toolset>
                   </msbuildToolsets>
                 </configuration>";

            string extnDir1 = null, extnDir2 = null, extnDir3 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"),
                                String.Format(extnTargetsFileContentTemplate, String.Empty, "FromExtn2", "<Import Project='$(MSBuildExtensionsPath32)\\bar\\extn2.proj' />"));
                extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("bar", "extn2.proj"),
                                String.Format(extnTargetsFileContentTemplate, 2, "FromExtn3", "<Import Project='$(MSBuildExtensionsPath64)\\xyz\\extn3.proj' />"));
                extnDir3 = GetNewExtensionsPathAndCreateFile("extensions3", Path.Combine("xyz", "extn3.proj"),
                                String.Format(extnTargetsFileContentTemplate, 3, String.Empty, String.Empty));

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent());

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(String.Format(configFileContents, extnDir1, extnDir2, extnDir3));

                var reader = GetStandardConfigurationReader();
                var projColln = GetProjectCollection();

                projColln.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projColln.RegisterLogger(logger);

                var project = projColln.LoadProject(mainProjectPath);
                Assert.True(project.Build("Main"));
                logger.AssertLogContains("Running FromExtn3");
                logger.AssertLogContains("Running FromExtn2");
                logger.AssertLogContains("Running FromExtn");
            }
            finally
            {
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }
                if (extnDir1 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir1, recursive: true);
                }
                if (extnDir2 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir2, recursive: true);
                }
                if (extnDir3 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir3, recursive: true);
                }
            }
        }

        // Fall-back path that has a property in it: $(FallbackExpandDir1)
        [Fact]
        public void ExpandExtensionsPathFallback()
        {
            string extnTargetsFileContentTemplate = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$(MSBuildExtensionsPath)\\foo\\extn.proj' Condition=""Exists('$(MSBuildExtensionsPath)\foo\extn.proj')"" />
                </Project>";

            var configFileContents = @"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""" + toolsVersionToUse + @""">
                     <toolset toolsVersion=""" + toolsVersionToUse + @""">
                       <property name=""MSBuildToolsPath"" value="".""/>
                       <property name=""MSBuildBinPath"" value="".""/>
                       <projectImportSearchPaths>
                         <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                           <property name=""MSBuildExtensionsPath"" value=""$(FallbackExpandDir1)"" />
                         </searchPaths>
                       </projectImportSearchPaths>
                      </toolset>
                   </msbuildToolsets>
                 </configuration>";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"),
                    extnTargetsFileContentTemplate);

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj",
                    GetMainTargetFileContent());

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(configFileContents);

                var reader = GetStandardConfigurationReader();
                var projectCollection = GetProjectCollection(new Dictionary<string, string> { ["FallbackExpandDir1"] = extnDir1 });

                projectCollection.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                var project = projectCollection.LoadProject(mainProjectPath);
                Assert.True(project.Build("Main"));
                logger.AssertLogContains("Running FromExtn");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(mainProjectPath);
                FileUtilities.DeleteDirectoryNoThrow(extnDir1, true);
            }
        }

        // Fall-back path that has a property in it: $(FallbackExpandDir1)
        [Fact]
        public void ExpandExtensionsPathFallbackInErrorMessage()
        {
            string extnTargetsFileContentTemplate = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$(MSBuildExtensionsPath)\\foo\\extn2.proj' Condition=""Exists('$(MSBuildExtensionsPath)\foo\extn.proj')"" />
                </Project>";

            var configFileContents = @"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""" + toolsVersionToUse + @""">
                     <toolset toolsVersion=""" + toolsVersionToUse + @""">
                       <property name=""MSBuildToolsPath"" value="".""/>
                       <property name=""MSBuildBinPath"" value="".""/>
                       <projectImportSearchPaths>
                         <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                           <property name=""MSBuildExtensionsPath"" value=""$(FallbackExpandDir1)"" />
                         </searchPaths>
                       </projectImportSearchPaths>
                      </toolset>
                   </msbuildToolsets>
                 </configuration>";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"),
                    extnTargetsFileContentTemplate);

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj",
                    GetMainTargetFileContent());

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(configFileContents);

                var reader = GetStandardConfigurationReader();
                var projectCollection = GetProjectCollection(new Dictionary<string, string> { ["FallbackExpandDir1"] = extnDir1 });

                projectCollection.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                Assert.Throws<InvalidProjectFileException>(() => projectCollection.LoadProject(mainProjectPath));

                // Expanded $(FallbackExpandDir) will appear in quotes in the log
                logger.AssertLogContains("\"" + extnDir1 + "\"");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(mainProjectPath);
                FileUtilities.DeleteDirectoryNoThrow(extnDir1, true);
            }
        }

        // Fall-back search path with custom variable
        [Fact]
        public void FallbackImportWithIndirectReference()
        {
            string mainTargetsFileContent = @"
               <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                   <PropertyGroup>
                       <VSToolsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v99</VSToolsPath>
                   </PropertyGroup>
                   <Import Project='$(VSToolsPath)\DNX\Microsoft.DNX.Props' Condition=""Exists('$(VSToolsPath)\DNX\Microsoft.DNX.Props')"" />
                   <Target Name='Main' DependsOnTargets='FromExtn' />
               </Project>";

            string extnTargetsFileContentTemplate = @"
               <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                   <Target Name='FromExtn'>
                       <Message Text='Running FromExtn'/>
                   </Target>
               </Project>";

            var configFileContents = @"
                <configuration>
                  <configSections>
                    <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                  </configSections>
                  <msbuildToolsets default=""" + toolsVersionToUse + @""">
                    <toolset toolsVersion=""" + toolsVersionToUse + @""">
                      <property name=""MSBuildToolsPath"" value="".""/>
                      <property name=""MSBuildBinPath"" value="".""/>
                      <projectImportSearchPaths>
                        <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                          <property name=""MSBuildExtensionsPath"" value=""$(FallbackExpandDir1)"" />
                          <property name=""VSToolsPath"" value=""$(FallbackExpandDir1)\Microsoft\VisualStudio\v99"" />
                        </searchPaths>
                      </projectImportSearchPaths>
                     </toolset>
                  </msbuildToolsets>
                </configuration>";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("Microsoft", "VisualStudio", "v99", "DNX", "Microsoft.DNX.Props"),
                    extnTargetsFileContentTemplate);

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", mainTargetsFileContent);

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(configFileContents);

                var reader = GetStandardConfigurationReader();
                var projectCollection = GetProjectCollection(new Dictionary<string, string> { ["FallbackExpandDir1"] = extnDir1 });

                projectCollection.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                var project = projectCollection.LoadProject(mainProjectPath);
                Assert.True(project.Build("Main"));
                logger.AssertLogContains("Running FromExtn");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(mainProjectPath);
                FileUtilities.DeleteDirectoryNoThrow(extnDir1, true);
            }
        }

        // Fall-back search path on a property that is not defined.
        [Fact]
        public void FallbackImportWithUndefinedProperty()
        {
            string mainTargetsFileContent = @"
               <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                   <Import Project='$(UndefinedProperty)\file.props' Condition=""Exists('$(UndefinedProperty)\file.props')"" />
                   <Target Name='Main' DependsOnTargets='FromExtn' />
               </Project>";

            string extnTargetsFileContentTemplate = @"
               <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                   <Target Name='FromExtn'>
                       <Message Text='Running FromExtn'/>
                   </Target>
               </Project>";

            var configFileContents = @"
                <configuration>
                  <configSections>
                    <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                  </configSections>
                  <msbuildToolsets default=""" + toolsVersionToUse + @""">
                    <toolset toolsVersion=""" + toolsVersionToUse + @""">
                      <property name=""MSBuildToolsPath"" value="".""/>
                      <property name=""MSBuildBinPath"" value="".""/>
                      <projectImportSearchPaths>
                        <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                          <property name=""UndefinedProperty"" value=""$(FallbackExpandDir1)"" />
                        </searchPaths>
                      </projectImportSearchPaths>
                     </toolset>
                  </msbuildToolsets>
                </configuration>";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("file.props"),
                    extnTargetsFileContentTemplate);

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", mainTargetsFileContent);

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(configFileContents);

                var reader = GetStandardConfigurationReader();
                var projectCollection = GetProjectCollection(new Dictionary<string, string> { ["FallbackExpandDir1"] = extnDir1 });

                projectCollection.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                var project = projectCollection.LoadProject(mainProjectPath);
                Assert.True(project.Build("Main"));
                logger.AssertLogContains("Running FromExtn");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(mainProjectPath);
                FileUtilities.DeleteDirectoryNoThrow(extnDir1, true);
            }
        }

        [Fact]
        public void FallbackImportWithFileNotFoundWhenPropertyNotDefined()
        {
            // Import something from $(UndefinedProperty)
            string mainTargetsFileContent = @"
               <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                   <Import Project='$(UndefinedProperty)\filenotfound.props' />
                   <Target Name='Main' DependsOnTargets='FromExtn' />
               </Project>";

            string extnDir1 = null;
            string mainProjectPath = null;

            try
            {
                // The path to "extensions1" fallback should exist, but the file doesn't need to
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("file.props"), string.Empty);

                // Implement fallback for UndefinedProperty, but don't define the property.
                var configFileContents = @"
                <configuration>
                  <configSections>
                    <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" />
                  </configSections>
                  <msbuildToolsets default=""" + toolsVersionToUse + @""">
                    <toolset toolsVersion=""" + toolsVersionToUse + @""">
                      <property name=""MSBuildToolsPath"" value="".""/>
                      <property name=""MSBuildBinPath"" value="".""/>
                      <projectImportSearchPaths>
                        <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                          <property name=""UndefinedProperty"" value=""" + extnDir1 + @""" />
                        </searchPaths>
                      </projectImportSearchPaths>
                     </toolset>
                  </msbuildToolsets>
                </configuration>";

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", mainTargetsFileContent);

                ToolsetConfigurationReaderTestHelper.WriteConfigFile(configFileContents);

                var reader = GetStandardConfigurationReader();
                var projectCollection = GetProjectCollection(new Dictionary<string, string> { ["FallbackExpandDir1"] = extnDir1 });

                projectCollection.ResetToolsetsForTests(reader);
                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                Assert.Throws<InvalidProjectFileException>(() => projectCollection.LoadProject(mainProjectPath));
                logger.AssertLogContains(@"MSB4226: The imported project """ + Path.Combine("$(UndefinedProperty)", "filenotfound.props")
                                            + @""" was not found. Also, tried to find");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(mainProjectPath);
                FileUtilities.DeleteDirectoryNoThrow(extnDir1, true);
            }
        }

        void CreateAndBuildProjectForImportFromExtensionsPath(string extnPathPropertyName, Action<Project, MockLogger> action)
        {
            string extnDir1 = null, extnDir2 = null, mainProjectPath = null;
            try
            {
                extnDir1 = GetNewExtensionsPathAndCreateFile("extensions1", Path.Combine("foo", "extn.proj"),
                                    GetExtensionTargetsFileContent1(extnPathPropertyName));
                extnDir2 = GetNewExtensionsPathAndCreateFile("extensions2", Path.Combine("bar", "extn2.proj"),
                                    GetExtensionTargetsFileContent2(extnPathPropertyName));

                mainProjectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", GetMainTargetFileContent(extnPathPropertyName));

                CreateAndBuildProjectForImportFromExtensionsPath(mainProjectPath, extnPathPropertyName, new string[] { extnDir1, extnDir2 },
                                                                null,
                                                                action);
            }
            finally
            {
                if (extnDir1 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir1, recursive: true);
                }
                if (extnDir2 != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(extnDir2, recursive: true);
                }
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }
            }
        }

        void CreateAndBuildProjectForImportFromExtensionsPath(string mainProjectPath, string extnPathPropertyName, string[] extnDirs, Action<string[]> setExtensionsPath,
                Action<Project, MockLogger> action)
        {
            try
            {
                var projColln = GetProjectCollection();

                projColln.ResetToolsetsForTests(WriteConfigFileAndGetReader(extnPathPropertyName, extnDirs));
                var logger = new MockLogger();
                projColln.RegisterLogger(logger);
                var project = projColln.LoadProject(mainProjectPath);

                action(project, logger);
            }
            finally
            {
                if (mainProjectPath != null)
                {
                    FileUtilities.DeleteNoThrow(mainProjectPath);
                }

                if (extnDirs != null)
                {
                    foreach (var extnDir in extnDirs)
                    {
                        FileUtilities.DeleteDirectoryNoThrow(extnDir, recursive: true);
                    }
                }
            }
        }

        private ToolsetConfigurationReader WriteConfigFileAndGetReader(string extnPathPropertyName, params string[] extnDirs)
        {
            string combinedExtnDirs = extnDirs != null ? String.Join(";", extnDirs) : String.Empty;

            ToolsetConfigurationReaderTestHelper.WriteConfigFile(@"
                 <configuration>
                   <configSections>
                     <section name=""msbuildToolsets"" type=""Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build"" />
                   </configSections>
                   <msbuildToolsets default=""" + toolsVersionToUse + @""">
                     <toolset toolsVersion=""" + toolsVersionToUse + @""">
                       <property name=""MSBuildToolsPath"" value=""."" />
                       <property name=""MSBuildBinPath"" value=""."" />
                       <projectImportSearchPaths>
                         <searchPaths os=""" + NativeMethodsShared.GetOSNameForExtensionsPath() + @""">
                           <property name=""" + extnPathPropertyName + @""" value=""" + combinedExtnDirs + @""" />
                         </searchPaths>
                       </projectImportSearchPaths>
                      </toolset>
                   </msbuildToolsets>
                 </configuration>");

            return GetStandardConfigurationReader();
        }

        private ProjectCollection GetProjectCollection(IDictionary<string, string> globalProperties = null)
        {
            ProjectCollection projColln;

            if (globalProperties == null)
            {
#if FEATURE_SYSTEM_CONFIGURATION
                projColln = new ProjectCollection();
#else
                projColln = new ProjectCollection(ToolsetDefinitionLocations.ConfigurationFile);
#endif
            }
            else
            {
#if FEATURE_SYSTEM_CONFIGURATION
                projColln = new ProjectCollection(globalProperties);
#else
                projColln = new ProjectCollection(globalProperties, loggers: null, ToolsetDefinitionLocations.ConfigurationFile);
#endif
            }

            return projColln;
        }

        string GetNewExtensionsPathAndCreateFile(string extnDirName, string relativeFilePath, string fileContents)
        {
            var extnDir = Path.Combine(ObjectModelHelpers.TempProjectDir, extnDirName);
            Directory.CreateDirectory(Path.Combine(extnDir, Path.GetDirectoryName(relativeFilePath)));
            File.WriteAllText(Path.Combine(extnDir, relativeFilePath), fileContents);

            return extnDir;
        }

        string GetMainTargetFileContent(string extensionsPathPropertyName = "MSBuildExtensionsPath")
        {
            string mainTargetsFileContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <Target Name='Main' DependsOnTargets='FromExtn'>
                        <Message Text='PropertyFromExtn1: $(PropertyFromExtn1)'/>
                    </Target>

                    <Import Project='$({0})\foo\extn.proj'/>
                </Project>";

            return String.Format(mainTargetsFileContent, extensionsPathPropertyName);
        }

        string GetExtensionTargetsFileContent1(string extensionsPathPropertyName = "MSBuildExtensionsPath")
        {
            string extnTargetsFileContent1 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn1>FooBar</PropertyFromExtn1>
                    </PropertyGroup>

                    <Target Name='FromExtn'>
                        <Message Text='Running FromExtn'/>
                    </Target>
                    <Import Project='$({0})\bar\extn2.proj'/>
                </Project>
                ";

            return String.Format(extnTargetsFileContent1, extensionsPathPropertyName);
        }

        string GetExtensionTargetsFileContent2(string extensionsPathPropertyName = "MSBuildExtensionsPath")
        {
            string extnTargetsFileContent2 = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    <PropertyGroup>
                        <PropertyFromExtn2>Abc</PropertyFromExtn2>
                    </PropertyGroup>

                    <Target Name='FromExtn2'>
                        <Message Text='Running FromExtn2'/>
                    </Target>
                </Project>
                ";

            return extnTargetsFileContent2;
        }

        private ToolsetConfigurationReader GetStandardConfigurationReader()
        {
            return new ToolsetConfigurationReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest);
        }
    }
}
