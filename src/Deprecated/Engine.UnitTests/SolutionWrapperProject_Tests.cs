// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Xml;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class SolutionWrapperProject_Tests
    {
        /// <summary>
        /// Verify the SolutionParser.AddNewErrorWarningMessageElement method
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void AddNewErrorWarningMessageElement()
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(
                "<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>" +
                    "<Target Name=`Build`>" +
                    "</Target>" +
                "</Project>",
                logger);

            Target target = project.Targets["Build"];

            SolutionWrapperProject.AddErrorWarningMessageElement(target, XMakeElements.message, true, "SolutionVenusProjectNoClean");
            SolutionWrapperProject.AddErrorWarningMessageElement(target, XMakeElements.warning, true, "SolutionParseUnknownProjectType", "proj1.csproj");
            SolutionWrapperProject.AddErrorWarningMessageElement(target, XMakeElements.error, true, "SolutionVCProjectNoPublish");

            project.Build(null, null);

            string code = null;
            string keyword = null;
            string text = ResourceUtilities.FormatResourceString(out code, out keyword, "SolutionParseUnknownProjectType", "proj1.csproj");

            // check the error event
            Assertion.AssertEquals(1, logger.Warnings.Count);
            BuildWarningEventArgs warning = logger.Warnings[0];

            Assertion.AssertEquals(text, warning.Message);
            Assertion.AssertEquals(code, warning.Code);
            Assertion.AssertEquals(keyword, warning.HelpKeyword);

            code = null;
            keyword = null;
            text = ResourceUtilities.FormatResourceString(out code, out keyword, "SolutionVCProjectNoPublish");

            // check the warning event
            Assertion.AssertEquals(1, logger.Errors.Count);
            BuildErrorEventArgs error = logger.Errors[0];

            Assertion.AssertEquals(text, error.Message);
            Assertion.AssertEquals(code, error.Code);
            Assertion.AssertEquals(keyword, error.HelpKeyword);

            code = null;
            keyword = null;
            text = ResourceUtilities.FormatResourceString(out code, out keyword, "SolutionVenusProjectNoClean");

            // check the message event
            Assertion.Assert("Log should contain the regular message", logger.FullLog.Contains(text));
        }

        /// <summary>
        /// Test to make sure we properly set the ToolsVersion attribute on the in-memory project based
        /// on the Solution File Format Version.
        /// </summary>
        [Test]
        public void EmitToolsVersionAttributeToInMemoryProject9()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
            {
                string solutionFileContents =
                    @"
                    Microsoft Visual Studio Solution File, Format Version 9.00
                    Global
                        GlobalSection(SolutionConfigurationPlatforms) = preSolution
                            Release|Any CPU = Release|Any CPU
                            Release|Win32 = Release|Win32
                            Other|Any CPU = Other|Any CPU
                            Other|Win32 = Other|Win32
                        EndGlobalSection
                    EndGlobal
                    ";

                SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

                Project msbuildProject = new Project();

                SolutionWrapperProject.Generate(solution, msbuildProject, "3.5", new BuildEventContext(0, 0, 0, 0));

                Assertion.AssertEquals("3.5", msbuildProject.DefaultToolsVersion);
            }
            else
            {
                Assert.Ignore(".NET Framework 3.5 is required for this test, but is not installed."); 
            }
        }

        /// <summary>
        /// Test to make sure we properly set the ToolsVersion attribute on the in-memory project based
        /// on the Solution File Format Version.
        /// </summary>
        [Test]
        public void EmitToolsVersionAttributeToInMemoryProject10()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 10.00
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Release|Any CPU = Release|Any CPU
                        Release|Win32 = Release|Win32
                        Other|Any CPU = Other|Any CPU
                        Other|Win32 = Other|Win32
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Project msbuildProject = new Project();

            SolutionWrapperProject.Generate(solution, msbuildProject, "4.0", new BuildEventContext(0, 0, 0, 0));

            Assertion.AssertEquals("4.0", msbuildProject.DefaultToolsVersion);
        }

        /// <summary>
        /// Test the SolutionWrapperProject.AddPropertyGroupForSolutionConfiguration method
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestAddPropertyGroupForSolutionConfiguration()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = CSConfig2|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.ActiveCfg = VCConfig1|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.Build.0 = VCConfig1|Win32
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Engine engine = new Engine();
            Project msbuildProject = new Project(engine);

            foreach (ConfigurationInSolution solutionConfiguration in solution.SolutionConfigurations)
            {
                SolutionWrapperProject.AddPropertyGroupForSolutionConfiguration(msbuildProject, solution, solutionConfiguration);
            }

            // Both projects configurations should be present for solution configuration "Debug|Mixed Platforms"
            msbuildProject.GlobalProperties.SetProperty("Configuration", "Debug");
            msbuildProject.GlobalProperties.SetProperty("Platform", "Mixed Platforms");

            string solutionConfigurationContents = msbuildProject.GetEvaluatedProperty("CurrentSolutionConfigurationContents");

            Assertion.Assert(solutionConfigurationContents.Contains("{6185CC21-BE89-448A-B3C0-D1C27112E595}"));
            Assertion.Assert(solutionConfigurationContents.Contains("CSConfig1|AnyCPU"));

            Assertion.Assert(solutionConfigurationContents.Contains("{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"));
            Assertion.Assert(solutionConfigurationContents.Contains("VCConfig1|Win32"));

            // Only the C# project should be present for solution configuration "Release|Any CPU", since the VC project
            // is missing
            msbuildProject.GlobalProperties.SetProperty("Configuration", "Release");
            msbuildProject.GlobalProperties.SetProperty("Platform", "Any CPU");

            solutionConfigurationContents = msbuildProject.GetEvaluatedProperty("CurrentSolutionConfigurationContents");

            Assertion.Assert(solutionConfigurationContents.Contains("{6185CC21-BE89-448A-B3C0-D1C27112E595}"));
            Assertion.Assert(solutionConfigurationContents.Contains("CSConfig2|AnyCPU"));

            Assertion.Assert(!solutionConfigurationContents.Contains("{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}"));
        }

        /// <summary>
        /// Test that the in memory project created from a solution file exposes an MSBuild property which,
        /// if set when building a solution, will be specified as the ToolsVersion on the MSBuild task when
        /// building the projects contained within the solution.
        /// </summary>
        [Test]
        public void ToolsVersionOverrideShouldBeSpecifiedOnMSBuildTaskInvocations()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = CSConfig2|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.ActiveCfg = VCConfig1|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.Build.0 = VCConfig1|Win32
                    EndGlobalSection
                EndGlobal
                ";

            // We're not passing in a /tv:xx switch, so the solution project will have tools version 3.5
            Project project = new Project();
            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);
            BuildEventContext buildEventContext = new BuildEventContext(0, 0, 0, 0);
            SolutionWrapperProject.Generate(solution, project, "3.5", buildEventContext);
            
            foreach (Target target in project.Targets)
            {
                foreach (XmlNode childNode in target.TargetElement)
                {
                    if (0 == String.Compare(childNode.Name, "MSBuild", StringComparison.OrdinalIgnoreCase))
                    {
                        // we found an MSBuild task invocation, now let's verify that it has the correct
                        // ToolsVersion parameter set
                        XmlAttribute toolsVersionAttribute = childNode.Attributes["ToolsVersion"];

                        Assertion.Assert(0 == String.Compare(
                                                            toolsVersionAttribute.Value,
                                                            "$(ProjectToolsVersion)", 
                                                            StringComparison.OrdinalIgnoreCase)
                                            );
                    }
                }
            }

        }

        /// <summary>
        /// Test the SolutionWrapperProject.Generate method with an invalid toolset -- will default to v4.0.
        /// </summary>
        /// <owner>jerelf</owner>
        [Test]
        public void ToolsVersionOverrideThrowsOnInvalidToolsVersion()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{6185CC21-BE89-448A-B3C0-D1C27112E595}'
                EndProject
                Project('{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}') = 'MainApp', 'MainApp\MainApp.vcxproj', '{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = CSConfig1|Any CPU
                        {6185CC21-BE89-448A-B3C0-D1C27112E595}.Release|Any CPU.ActiveCfg = CSConfig2|Any CPU
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.ActiveCfg = VCConfig1|Win32
                        {A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Debug|Mixed Platforms.Build.0 = VCConfig1|Win32
                    EndGlobalSection
                EndGlobal
                ";

            string oldUseNoCacheValue = Environment.GetEnvironmentVariable("MSBuildUseNoSolutionCache");

            try
            {
                // We want to avoid using the solution cache -- it could lead to circumstances where detritus left 
                // on the disk leads us down paths we didn't mean to go.  
                Environment.SetEnvironmentVariable("MSBuildUseNoSolutionCache", "1");

                // We're not passing in a /tv:xx switch, so the solution project will have tools version 3.5
                Project project = new Project();
                SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);
                BuildEventContext buildEventContext = new BuildEventContext(0, 0, 0, 0);
            
                SolutionWrapperProject.Generate(solution, project, "invalid", buildEventContext);

                Assertion.AssertEquals("4.0", project.DefaultToolsVersion);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBuildUseNoSolutionCache", oldUseNoCacheValue);
            }
        }
            
        /// <summary>
        /// Test the SolutionWrapperProject.AddPropertyGroupForSolutionConfiguration method
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestDisambiguateProjectTargetName()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'Build', 'Build\Build.csproj', '{21397922-C38F-4A0E-B950-77B3FBD51881}'
                EndProject
                Global
                        GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                        EndGlobalSection
                        GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {21397922-C38F-4A0E-B950-77B3FBD51881}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {21397922-C38F-4A0E-B950-77B3FBD51881}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {21397922-C38F-4A0E-B950-77B3FBD51881}.Release|Any CPU.ActiveCfg = Release|Any CPU
                                {21397922-C38F-4A0E-B950-77B3FBD51881}.Release|Any CPU.Build.0 = Release|Any CPU
                        EndGlobalSection
                        GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                        EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Project msbuildProject = new Project();

            SolutionWrapperProject.Generate(solution, msbuildProject, null, null);

            Assertion.AssertNotNull(msbuildProject.Targets["Build"]);
            Assertion.AssertNotNull(msbuildProject.Targets["Solution:Build"]);
            Assertion.AssertNotNull(msbuildProject.Targets["Solution:Build:Clean"]);
            Assertion.AssertNotNull(msbuildProject.Targets["Solution:Build:Rebuild"]);
            Assertion.AssertNotNull(msbuildProject.Targets["Solution:Build:Publish"]);
            Assertion.AssertEquals(null, msbuildProject.Targets["Build"].TargetElement.ChildNodes[0].Attributes["Targets"]);
            Assertion.AssertEquals("Clean", msbuildProject.Targets["Clean"].TargetElement.ChildNodes[0].Attributes["Targets"].Value);
            Assertion.AssertEquals("Rebuild", msbuildProject.Targets["Rebuild"].TargetElement.ChildNodes[0].Attributes["Targets"].Value);
            Assertion.AssertEquals("Publish", msbuildProject.Targets["Publish"].TargetElement.ChildNodes[0].Attributes["Targets"].Value);
            Assertion.AssertEquals("@(BuildLevel0)", msbuildProject.Targets["Build"].TargetElement.ChildNodes[0].Attributes["Projects"].Value);
            Assertion.AssertEquals("@(BuildLevel0)", msbuildProject.Targets["Clean"].TargetElement.ChildNodes[0].Attributes["Projects"].Value);
            Assertion.AssertEquals("@(BuildLevel0)", msbuildProject.Targets["Rebuild"].TargetElement.ChildNodes[0].Attributes["Projects"].Value);
            Assertion.AssertEquals("@(BuildLevel0)", msbuildProject.Targets["Publish"].TargetElement.ChildNodes[0].Attributes["Projects"].Value);

            // Here we check that the set of standard entry point targets in the solution project
            // matches those defined in ProjectInSolution.projectNamesToDisambiguate = { "Build", "Rebuild", "Clean", "Publish" };
            int countOfStandardTargets = 0;
            foreach (Target t in msbuildProject.Targets)
            {
                if (!t.Name.Contains(":"))
                {
                    countOfStandardTargets += 1;
                }
            }

            // NOTE: ValidateSolutionConfiguration and ValidateToolsVersions are always added, so we need to add two extras
            Assertion.AssertEquals(ProjectInSolution.projectNamesToDisambiguate.Length + 2, countOfStandardTargets);
        }
        
        /// <summary>
        /// Tests the algorithm for choosing default configuration/platform values for solutions
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestConfigurationPlatformDefaults1()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Debug|Win32 = Debug|Win32
                        Release|Any CPU = Release|Any CPU
                        Release|Mixed Platforms = Release|Mixed Platforms
                        Release|Win32 = Release|Win32
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Project msbuildProject = new Project();
            SolutionWrapperProject.Generate(solution, msbuildProject, null, null);
            
            // Default for Configuration is "Debug", if present
            Assertion.AssertEquals("Debug", msbuildProject.GetEvaluatedProperty("Configuration"));

            // Default for Platform is "Mixed Platforms", if present
            Assertion.AssertEquals("Mixed Platforms", msbuildProject.GetEvaluatedProperty("Platform"));
        }

        /// <summary>
        /// Tests the algorithm for choosing default configuration/platform values for solutions
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestConfigurationPlatformDefaults2()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Release|Any CPU = Release|Any CPU
                        Release|Win32 = Release|Win32
                        Other|Any CPU = Other|Any CPU
                        Other|Win32 = Other|Win32
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Project msbuildProject = new Project();

            SolutionWrapperProject.Generate(solution, msbuildProject, null, null);

            // If "Debug" is not present, just pick the first configuration name
            Assertion.AssertEquals("Release", msbuildProject.GetEvaluatedProperty("Configuration"));

            // if "Mixed Platforms" is not present, just pick the first platform name
            Assertion.AssertEquals("Any CPU", msbuildProject.GetEvaluatedProperty("Platform"));
        }

        /// <summary>
        /// Tests the algorithm for choosing default Venus configuration values for solutions
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestVenusConfigurationDefaults()
        {
            Project msbuildProject = CreateVenusSolutionProject();

            // ASP.NET configuration should match the selected solution configuration
            msbuildProject.GlobalProperties.SetProperty("Configuration", "Debug");
            Assertion.AssertEquals("Debug", msbuildProject.GetEvaluatedProperty("AspNetConfiguration"));

            msbuildProject.GlobalProperties.SetProperty("Configuration", "Release");
            Assertion.AssertEquals("Release", msbuildProject.GetEvaluatedProperty("AspNetConfiguration"));

            msbuildProject.GlobalProperties.SetProperty("Configuration", "Other");
            Assertion.AssertEquals("Other", msbuildProject.GetEvaluatedProperty("AspNetConfiguration"));

            // Check that the two standard Asp.net configurations are represented on the targets
            Assertion.Assert(msbuildProject.Targets["C:\\solutions\\WebSite2\\"].Condition.Contains("'$(Configuration)' == 'Release'"));
            Assertion.Assert(msbuildProject.Targets["C:\\solutions\\WebSite2\\"].Condition.Contains("'$(Configuration)' == 'Debug'"));
        }

        [Test]
        public void DefaultTargetFrameworkVersion()
        {
            Project msbuildProject = CreateVenusSolutionProject();

            // v3.5 by default
            Assertion.AssertEquals("v4.0", msbuildProject.EvaluatedProperties["TargetFrameworkVersion"].Value);
            // may be user defined 
            msbuildProject.SetProperty("TargetFrameworkVersion", "userdefined");
            Assertion.AssertEquals("userdefined", msbuildProject.EvaluatedProperties["TargetFrameworkVersion"].Value);
            // v2.0 if MSBuildToolsVersion is 2.0
            msbuildProject.SetProperty("TargetFrameworkVersion", String.Empty);
            msbuildProject.ToolsVersion = "2.0";
            Assertion.AssertEquals("v2.0", msbuildProject.EvaluatedProperties["TargetFrameworkVersion"].Value);
        }

        /// <summary>
        /// Tests the algorithm for choosing target framework paths for ResolveAssemblyReferences for Venus
        /// </summary>
        [Test]
        public void TestTargetFrameworkPaths0()
        {
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("TargetFrameworkVersion", "v2.0");

            Project msbuildProject = CreateVenusSolutionProject(globalProperties, "4.0");

            // ToolsVersion is 2.0, TargetFrameworkVersion is v2.0 --> one item pointing to v2.0
            msbuildProject.ToolsVersion = "4.0";

            bool success = msbuildProject.Build("GetFrameworkPathAndRedistList");
            Assertion.AssertEquals(true, success);

            AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV20);
            AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 1);
        }

        /// <summary>
        /// Tests the algorithm for choosing target framework paths for ResolveAssemblyReferences for Venus
        /// </summary>
        [Test]
        public void TestTargetFrameworkPaths1()
        {
            Project msbuildProject = CreateVenusSolutionProject();

            // ToolsVersion is 3.5, TargetFrameworkVersion is v2.0 --> one item pointing to v2.0
            msbuildProject.SetProperty("TargetFrameworkVersion", "v2.0");
            bool success = msbuildProject.Build("GetFrameworkPathAndRedistList");
            Assertion.AssertEquals(true, success);

            AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV20);
            AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 1);
        }

        /// <summary>
        /// Tests the algorithm for choosing target framework paths for ResolveAssemblyReferences for Venus
        /// </summary>
        [Test]
        public void TestTargetFrameworkPaths2()
        {
            Project msbuildProject = CreateVenusSolutionProject();

            // ToolsVersion is 3.5, TargetFrameworkVersion is v3.5 --> items for v2.0 and v3.5
            msbuildProject.SetProperty("TargetFrameworkVersion", "v3.5");
            bool success = msbuildProject.Build("GetFrameworkPathAndRedistList");
            Assertion.AssertEquals(true, success);
            
            AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV20);
            if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null && FrameworkLocationHelper.PathToDotNetFrameworkV30 != null)
            {
                AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV35);
                AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV30);
                AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 3);
            }
            else if (FrameworkLocationHelper.PathToDotNetFrameworkV30 != null)
            {
                AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV30);
                AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 2);
            }
            else if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
            {
                AssertProjectContainsItem(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", FrameworkLocationHelper.PathToDotNetFrameworkV35);
                AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 2);
            }
            else
            {
                AssertProjectItemNameCount(msbuildProject, "_CombinedTargetFrameworkDirectoriesItem", 1);
            }
        }

        private static Project CreateVenusSolutionProject()
        {
            return CreateVenusSolutionProject(null);
        }

        private static Project CreateVenusSolutionProject(BuildPropertyGroup globalProperties)
        {
            return CreateVenusSolutionProject(globalProperties, "4.0");
        }

        private static Project CreateVenusSolutionProject(BuildPropertyGroup globalProperties, string toolsVersion)
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\solutions\WebSite2\', '..\..\solutions\WebSite2\', '{F90528C4-6989-4D33-AFE8-F53173597CC2}'
                    ProjectSection(WebsiteProperties) = preProject
                        Debug.AspNetCompiler.VirtualPath = '/WebSite2'
                        Debug.AspNetCompiler.PhysicalPath = '..\..\solutions\WebSite2\'
                        Debug.AspNetCompiler.TargetPath = 'PrecompiledWeb\WebSite2\'
                        Debug.AspNetCompiler.Updateable = 'true'
                        Debug.AspNetCompiler.ForceOverwrite = 'true'
                        Debug.AspNetCompiler.FixedNames = 'true'
                        Debug.AspNetCompiler.Debug = 'True'
                        Release.AspNetCompiler.VirtualPath = '/WebSite2'
                        Release.AspNetCompiler.PhysicalPath = '..\..\solutions\WebSite2\'
                        Release.AspNetCompiler.TargetPath = 'PrecompiledWeb\WebSite2\'
                        Release.AspNetCompiler.Updateable = 'true'
                        Release.AspNetCompiler.ForceOverwrite = 'true'
                        Release.AspNetCompiler.FixedNames = 'true'
                        Release.AspNetCompiler.Debug = 'False'
                        VWDPort = '2776'
                        DefaultWebSiteLanguage = 'Visual C#'
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {F90528C4-6989-4D33-AFE8-F53173597CC2}.Debug|Any CPU.ActiveCfg = Debug|.NET
                        {F90528C4-6989-4D33-AFE8-F53173597CC2}.Debug|Any CPU.Build.0 = Debug|.NET
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);

            Engine engine = new Engine(globalProperties);
            Project msbuildProject = new Project(engine);

            SolutionWrapperProject.Generate(solution, msbuildProject, toolsVersion, null);
            return msbuildProject;
        }
  
        private void AssertProjectContainsItem(Project msbuildProject, string itemName, string itemSpec)
        {
            BuildItemGroup itemGroup = (BuildItemGroup)msbuildProject.EvaluatedItemsByName[itemName];
            Assertion.AssertNotNull(itemGroup);

            foreach(BuildItem item in itemGroup)
            {
                if (item.Name == itemName && item.EvaluatedItemSpec == itemSpec)
                {
                    return;
                }
            }

            Assertion.Assert(false);
        }

        private void AssertProjectItemNameCount(Project msbuildProject, string itemName, int count)
        {
            BuildItemGroup itemGroup = (BuildItemGroup)msbuildProject.EvaluatedItemsByName[itemName];
            Assertion.AssertNotNull(itemGroup);
            Assertion.AssertEquals(count, itemGroup.Count);
        }

        /// <summary>
        /// Test the PredictActiveSolutionConfigurationName method
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestPredictSolutionConfigurationName()
        {
            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Release|Mixed Platforms = Release|Mixed Platforms
                        Release|Win32 = Release|Win32
                        Debug|Mixed Platforms = Debug|Mixed Platforms
                        Debug|Win32 = Debug|Win32
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);
            Engine engine = new Engine();

            Assertion.AssertEquals("Debug|Mixed Platforms", SolutionWrapperProject.PredictActiveSolutionConfigurationName(solution, engine));

            engine.GlobalProperties.SetProperty("Configuration", "Release");
            Assertion.AssertEquals("Release|Mixed Platforms", SolutionWrapperProject.PredictActiveSolutionConfigurationName(solution, engine));

            engine.GlobalProperties.SetProperty("Platform", "Win32");
            Assertion.AssertEquals("Release|Win32", SolutionWrapperProject.PredictActiveSolutionConfigurationName(solution, engine));

            engine.GlobalProperties.SetProperty("Configuration", "Nonexistent");
            Assertion.AssertEquals(null, SolutionWrapperProject.PredictActiveSolutionConfigurationName(solution, engine));
        }

        /// <summary>
        /// We had a bug where turning on the environment variable MSBuildEmitSolution=1 caused
        /// a VerifyThrow to get fired in the engine, because the temp project that is saved
        /// to disk gets loaded into the engine when it shouldn't have.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SolutionParserShouldNotIncreaseNumberOfProjectsLoadedByHost()
        {
            string oldValueForMSBuildEmitSolution = Environment.GetEnvironmentVariable("MSBuildEmitSolution");
            Environment.SetEnvironmentVariable("MSBuildEmitSolution", "1");

            string solutionFileContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{F184B08F-C81C-45F6-A57F-5ABD9991F28F}') = 'ConsoleApplication1', 'ConsoleApplication1\ConsoleApplication1.vbproj', '{AB3413A6-D689-486D-B7F0-A095371B3F13}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|AnyCPU = Debug|AnyCPU
                        Release|AnyCPU = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.ActiveCfg = Debug|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Debug|AnyCPU.Build.0 = Debug|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.ActiveCfg = Release|AnyCPU
                        {AB3413A6-D689-486D-B7F0-A095371B3F13}.Release|AnyCPU.Build.0 = Release|AnyCPU
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                ";

            SolutionParser solution = SolutionParser_Tests.ParseSolutionHelper(solutionFileContents);
            Engine engine = new Engine();
            Project project = new Project(engine, null);

            // This project considers itself loaded-by-host. Setting a file name on it, causes it to 
            // ensure the engine believes it is loaded-by-host...
            project.FullFileName = "my project";

            Assertion.AssertEquals(1, engine.ProjectsLoadedByHost.Count);

            // Create a bogus cache file in the same place -- just to exercise the solution wrapper code that creates a new project
            string solutionCacheFile = solution.SolutionFile + ".cache";
            using (StreamWriter writer = new StreamWriter(solutionCacheFile))
            {
                writer.WriteLine("xxx");
            }
            SolutionWrapperProject.Generate(solution, project, null, null);

            Assertion.AssertEquals(1, engine.ProjectsLoadedByHost.Count);

            // Clean up.  Delete temp files and reset environment variables.
            Assertion.Assert("Solution parser should have written in-memory project to disk",
                File.Exists(solution.SolutionFile + ".proj"));
            File.Delete(solution.SolutionFile + ".proj");
            File.Delete(solutionCacheFile);

            Environment.SetEnvironmentVariable("MSBuildEmitSolution", oldValueForMSBuildEmitSolution);
        }

        /// <summary>
        /// Make sure that we output a warning and don't build anything when we're given an invalid
        /// solution configuration and SkipInvalidConfigurations is set to true.
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void TestSkipInvalidConfigurationsCase()
        {
            string tmpFileName = Path.GetTempFileName();
            File.Delete(tmpFileName);
            string projectFilePath = tmpFileName + ".sln";

            string solutionContents =
                @"
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project('{E24C65DC-7377-472B-9ABA-BC803B73C61A}') = 'C:\solutions\WebSite2\', '..\..\solutions\WebSite2\', '{F90528C4-6989-4D33-AFE8-F53173597CC2}'
                    ProjectSection(WebsiteProperties) = preProject
                        Debug.AspNetCompiler.VirtualPath = '/WebSite2'
                        Debug.AspNetCompiler.PhysicalPath = '..\..\solutions\WebSite2\'
                        Debug.AspNetCompiler.TargetPath = 'PrecompiledWeb\WebSite2\'
                        Debug.AspNetCompiler.Updateable = 'true'
                        Debug.AspNetCompiler.ForceOverwrite = 'true'
                        Debug.AspNetCompiler.FixedNames = 'true'
                        Debug.AspNetCompiler.Debug = 'True'
                        Release.AspNetCompiler.VirtualPath = '/WebSite2'
                        Release.AspNetCompiler.PhysicalPath = '..\..\solutions\WebSite2\'
                        Release.AspNetCompiler.TargetPath = 'PrecompiledWeb\WebSite2\'
                        Release.AspNetCompiler.Updateable = 'true'
                        Release.AspNetCompiler.ForceOverwrite = 'true'
                        Release.AspNetCompiler.FixedNames = 'true'
                        Release.AspNetCompiler.Debug = 'False'
                        VWDPort = '2776'
                        DefaultWebSiteLanguage = 'Visual C#'
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {F90528C4-6989-4D33-AFE8-F53173597CC2}.Debug|Any CPU.ActiveCfg = Debug|.NET
                        {F90528C4-6989-4D33-AFE8-F53173597CC2}.Debug|Any CPU.Build.0 = Debug|.NET
                    EndGlobalSection
                EndGlobal";

            File.WriteAllText(projectFilePath, solutionContents.Replace('\'', '"'));

            try
            {
                MockLogger logger = new MockLogger();
                Engine engine = new Engine();
                engine.RegisterLogger(logger);

                Project solutionWrapperProject = new Project(engine);
                solutionWrapperProject.Load(projectFilePath);

                solutionWrapperProject.SetProperty("Configuration", "Nonexistent");
                solutionWrapperProject.SetProperty("SkipInvalidConfigurations", "true");
                solutionWrapperProject.ToolsVersion = "4.0";

                // Build should complete successfully even with an invalid solution config if SkipInvalidConfigurations is true
                Assertion.AssertEquals(true, solutionWrapperProject.Build(null, null));

                // We should get the invalid solution configuration warning
                Assertion.AssertEquals(1, logger.Warnings.Count);
                BuildWarningEventArgs warning = logger.Warnings[0];

                // Don't look at warning.Code here -- it may be null if PseudoLoc has messed
                // with our resource strings. The code will still be in the log -- it just wouldn't get
                // pulled out into the code field.
                logger.AssertLogContains("MSB4126");

                // No errors expected
                Assertion.AssertEquals(0, logger.Errors.Count);
            }
            finally
            {
                File.Delete(projectFilePath);
            }
        }

        /// <summary>
        /// Convert passed in solution file to an MSBuild project. This method is used by Sln2Proj
        /// </summary>
        /// <owner>vladf</owner>
        public bool ConvertSLN2Proj(string nameSolutionFile)
        {
            // Set the environment variable to cause the SolutionWrapperProject to emit the project to disk
            string oldValueForMSBuildEmitSolution = Environment.GetEnvironmentVariable("MSBuildEmitSolution");
            Environment.SetEnvironmentVariable("MSBuildEmitSolution", "1");

            if (nameSolutionFile == null || !File.Exists(nameSolutionFile))
            {
                return false;
            }

            // Parse the solution
            SolutionParser solution = new SolutionParser();
            solution.SolutionFile = nameSolutionFile;
            solution.ParseSolutionFile();

            // Generate the in-memory MSBuild project and output it to disk
            Project project = new Project();
            SolutionWrapperProject.Generate(solution, project, "4.0", null);


            //Reset the environment variable
            Environment.SetEnvironmentVariable("MSBuildEmitSolution", oldValueForMSBuildEmitSolution);

            return true;
        }
    }
}
