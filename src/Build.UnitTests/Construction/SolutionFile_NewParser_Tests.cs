// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Construction
{
    public class SolutionFile_NewParser_Tests
    {
        public ITestOutputHelper TestOutputHelper { get; }

        public SolutionFile_NewParser_Tests(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        /// <summary>
        /// Tests to see that all the data/properties are correctly parsed out of a Venus
        /// project in a .SLN. This can be checked only here because of AspNetConfigurations protection level.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProjectWithWebsiteProperties(bool convertToSlnx)
        {
            string solutionFileContents =
                """
                Microsoft Visual Studio Solution File, Format Version 9.00
                # Visual Studio 2005
                Project(`{E24C65DC-7377-472B-9ABA-BC803B73C61A}`) = `C:\WebSites\WebApplication3\`, `C:\WebSites\WebApplication3\`, `{464FD0B9-E335-4677-BE1E-6B2F982F4D86}`
                    ProjectSection(WebsiteProperties) = preProject
                        ProjectReferences = `{FD705688-88D1-4C22-9BFF-86235D89C2FC}|CSCla;ssLibra;ry1.dll;{F0726D09-042B-4A7A-8A01-6BED2422BD5D}|VCClassLibrary1.dll;`
                        Frontpage = false
                            Debug.AspNetCompiler.VirtualPath = `/publishfirst`
                            Debug.AspNetCompiler.PhysicalPath = `..\rajeev\temp\websites\myfirstwebsite\`
                            Debug.AspNetCompiler.TargetPath = `..\rajeev\temp\publishfirst\`
                            Debug.AspNetCompiler.ForceOverwrite = `true`
                            Debug.AspNetCompiler.Updateable = `false`
                            Debug.AspNetCompiler.Debug = `true`
                            Debug.AspNetCompiler.KeyFile = `debugkeyfile.snk`
                            Debug.AspNetCompiler.KeyContainer = `12345.container`
                            Debug.AspNetCompiler.DelaySign = `true`
                            Debug.AspNetCompiler.AllowPartiallyTrustedCallers = `false`
                            Debug.AspNetCompiler.FixedNames = `debugfixednames`
                            Release.AspNetCompiler.VirtualPath = `/publishfirst_release`
                            Release.AspNetCompiler.PhysicalPath = `..\rajeev\temp\websites\myfirstwebsite_release\`
                            Release.AspNetCompiler.TargetPath = `..\rajeev\temp\publishfirst_release\`
                            Release.AspNetCompiler.ForceOverwrite = `true`
                            Release.AspNetCompiler.Updateable = `true`
                            Release.AspNetCompiler.Debug = `false`
                        VWDPort = 63496
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|.NET = Debug|.NET
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {464FD0B9-E335-4677-BE1E-6B2F982F4D86}.Debug|.NET.ActiveCfg = Debug|.NET
                        {464FD0B9-E335-4677-BE1E-6B2F982F4D86}.Debug|.NET.Build.0 = Debug|.NET
                    EndGlobalSection
                    GlobalSection(SolutionProperties) = preSolution
                        HideSolutionNode = FALSE
                    EndGlobalSection
                EndGlobal
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents.Replace('`', '"'), convertToSlnx);

            solution.ProjectsInOrder.ShouldHaveSingleItem();

            solution.ProjectsInOrder[0].ProjectType.ShouldBe(SolutionProjectType.WebProject);
            solution.ProjectsInOrder[0].ProjectName.ShouldBe(@"C:\WebSites\WebApplication3\");
            solution.ProjectsInOrder[0].RelativePath.ShouldBe(ConvertToUnixPathIfNeeded(@"C:\WebSites\WebApplication3\"));
            solution.ProjectsInOrder[0].Dependencies.Count.ShouldBe(2);
            solution.ProjectsInOrder[0].ParentProjectGuid.ShouldBeNull();
            solution.ProjectsInOrder[0].GetUniqueProjectName().ShouldBe(@"C:\WebSites\WebApplication3\");

            Hashtable aspNetCompilerParameters = solution.ProjectsInOrder[0].AspNetConfigurations;
            AspNetCompilerParameters debugAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Debug"];
            AspNetCompilerParameters releaseAspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParameters["Release"];

            debugAspNetCompilerParameters.aspNetVirtualPath.ShouldBe(@"/publishfirst");
            debugAspNetCompilerParameters.aspNetPhysicalPath.ShouldBe(@"..\rajeev\temp\websites\myfirstwebsite\");
            debugAspNetCompilerParameters.aspNetTargetPath.ShouldBe(@"..\rajeev\temp\publishfirst\");
            debugAspNetCompilerParameters.aspNetForce.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetUpdateable.ShouldBe(@"false");
            debugAspNetCompilerParameters.aspNetDebug.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetKeyFile.ShouldBe(@"debugkeyfile.snk");
            debugAspNetCompilerParameters.aspNetKeyContainer.ShouldBe(@"12345.container");
            debugAspNetCompilerParameters.aspNetDelaySign.ShouldBe(@"true");
            debugAspNetCompilerParameters.aspNetAPTCA.ShouldBe(@"false");
            debugAspNetCompilerParameters.aspNetFixedNames.ShouldBe(@"debugfixednames");

            releaseAspNetCompilerParameters.aspNetVirtualPath.ShouldBe(@"/publishfirst_release");
            releaseAspNetCompilerParameters.aspNetPhysicalPath.ShouldBe(@"..\rajeev\temp\websites\myfirstwebsite_release\");
            releaseAspNetCompilerParameters.aspNetTargetPath.ShouldBe(@"..\rajeev\temp\publishfirst_release\");
            releaseAspNetCompilerParameters.aspNetForce.ShouldBe(@"true");
            releaseAspNetCompilerParameters.aspNetUpdateable.ShouldBe(@"true");
            releaseAspNetCompilerParameters.aspNetDebug.ShouldBe(@"false");
            releaseAspNetCompilerParameters.aspNetKeyFile.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetKeyContainer.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetDelaySign.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetAPTCA.ShouldBe("");
            releaseAspNetCompilerParameters.aspNetFixedNames.ShouldBe("");

            List<string> aspNetProjectReferences = solution.ProjectsInOrder[0].ProjectReferences;
            aspNetProjectReferences.Count.ShouldBe(2);
            aspNetProjectReferences[0].ShouldBe("{FD705688-88D1-4C22-9BFF-86235D89C2FC}");
            aspNetProjectReferences[1].ShouldBe("{F0726D09-042B-4A7A-8A01-6BED2422BD5D}");
        }

        /// <summary>
        /// The solution configurations must be returned in the order in which the solution file declares them, even
        /// when the first project doesn't map every solution configuration. Otherwise the default configuration and
        /// platform - which are used when none is specified on the command line - would depend on the order in which
        /// the projects happen to declare their own configurations.
        /// </summary>
        [Fact]
        public void SolutionConfigurationsAreOrderedByTheSolutionAndNotByTheProjects()
        {
            string solutionFileContents =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{11111111-1111-1111-1111-111111111111}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.csproj', '{22222222-2222-2222-2222-222222222222}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|x86 = Debug|x86
                        Debug|x64 = Debug|x64
                        Release|x86 = Release|x86
                        Release|x64 = Release|x64
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {11111111-1111-1111-1111-111111111111}.Debug|x64.ActiveCfg = Debug|x64
                        {11111111-1111-1111-1111-111111111111}.Release|x86.ActiveCfg = Release|x86
                        {11111111-1111-1111-1111-111111111111}.Release|x64.ActiveCfg = Release|x64
                        {22222222-2222-2222-2222-222222222222}.Debug|x86.ActiveCfg = Debug|x86
                        {22222222-2222-2222-2222-222222222222}.Debug|x64.ActiveCfg = Debug|x64
                        {22222222-2222-2222-2222-222222222222}.Release|x86.ActiveCfg = Release|x86
                        {22222222-2222-2222-2222-222222222222}.Release|x64.ActiveCfg = Release|x64
                    EndGlobalSection
                EndGlobal
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents);

            solution.SolutionConfigurations.Select(configuration => configuration.FullName)
                .ShouldBe(["Debug|x86", "Debug|x64", "Release|x86", "Release|x64"]);

            solution.GetDefaultConfigurationName().ShouldBe("Debug");
            solution.GetDefaultPlatformName().ShouldBe("x86");
        }

        /// <summary>
        /// Solution configurations parsed from a .slnx file must be deterministic too. The solution model normalizes
        /// the order of the build types and platforms declared in a .slnx file, so the solution configurations follow
        /// that normalized order rather than the order in which the projects declare their own configurations.
        /// </summary>
        [Fact]
        public void SolutionConfigurationsFromSlnxAreOrderedByTheSolutionAndNotByTheProjects()
        {
            string solutionFileContents =
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary1', 'ClassLibrary1\ClassLibrary1.csproj', '{11111111-1111-1111-1111-111111111111}'
                EndProject
                Project('{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}') = 'ClassLibrary2', 'ClassLibrary2\ClassLibrary2.csproj', '{22222222-2222-2222-2222-222222222222}'
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|x86 = Debug|x86
                        Debug|x64 = Debug|x64
                        Release|x86 = Release|x86
                        Release|x64 = Release|x64
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {11111111-1111-1111-1111-111111111111}.Debug|x64.ActiveCfg = Debug|x64
                        {11111111-1111-1111-1111-111111111111}.Release|x86.ActiveCfg = Release|x86
                        {11111111-1111-1111-1111-111111111111}.Release|x64.ActiveCfg = Release|x64
                        {22222222-2222-2222-2222-222222222222}.Debug|x86.ActiveCfg = Debug|x86
                        {22222222-2222-2222-2222-222222222222}.Debug|x64.ActiveCfg = Debug|x64
                        {22222222-2222-2222-2222-222222222222}.Release|x86.ActiveCfg = Release|x86
                        {22222222-2222-2222-2222-222222222222}.Release|x64.ActiveCfg = Release|x64
                    EndGlobalSection
                EndGlobal
                """;

            SolutionFile solution = ParseSolutionHelper(solutionFileContents, convertToSlnx: true);

            solution.SolutionConfigurations.Select(configuration => configuration.FullName)
                .ShouldBe(["Debug|x64", "Debug|x86", "Release|x64", "Release|x86"]);

            solution.GetDefaultConfigurationName().ShouldBe("Debug");
            solution.GetDefaultPlatformName().ShouldBe("x64");
        }

        /// <summary>
        /// Helper method to create a SolutionFile object, and call it to parse the SLN file
        /// represented by the string contents passed in. Optionally can convert the SLN to SLNX and then parse the solution.
        /// </summary>
        private static SolutionFile ParseSolutionHelper(string solutionFileContents, bool convertToSlnx = false)
        {
            solutionFileContents = solutionFileContents.Replace('\'', '"');
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                solutionFileContents = solutionFileContents.Replace('\'', '"');
                testEnvironment.SetEnvironmentVariable("MSBUILD_PARSE_SLN_WITH_SOLUTIONPERSISTENCE", "1");
                TransientTestFile sln = testEnvironment.CreateFile(FileUtilities.GetTemporaryFileName(".sln"), solutionFileContents);
                string solutionPath = convertToSlnx ? ConvertToSlnx(sln.Path) : sln.Path;
                SolutionFile solutionFile = new SolutionFile { FullPath = solutionPath };
                solutionFile.ParseUsingNewParser();
                return solutionFile;
            }
        }

        internal static string ConvertToSlnx(string slnPath)
        {
            string slnxPath = slnPath + "x";
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(slnPath).ShouldNotBeNull();
            SolutionModel solutionModel = serializer.OpenAsync(slnPath, CancellationToken.None).Result;
            SolutionSerializers.SlnXml.SaveAsync(slnxPath, solutionModel, CancellationToken.None).Wait();
            return slnxPath;
        }

        private static string ConvertToUnixPathIfNeeded(string path)
        {
            // In the new parser, ProjectModel.FilePath is converted to Unix-style.
            return !NativeMethodsShared.IsWindows ? path.Replace('\\', '/') : path;
        }
    }
}
