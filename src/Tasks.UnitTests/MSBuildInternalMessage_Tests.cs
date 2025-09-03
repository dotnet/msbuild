// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class MSBuildInternalMessage_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public MSBuildInternalMessage_Tests(ITestOutputHelper testOutput) => _testOutput = testOutput;

        [Theory]
        [InlineData(true, true, "CommonSdk.Prefer32BitAndPreferNativeArm64Enabled", false)]
        [InlineData(false, false, "CommonSdk.PlatformIsAnyCPUAndPreferNativeArm64Enabled", true, new[] { "Release" })]
        public void E2EScenarioTests(bool prefer32, bool isPlatformAnyCpu, string expectedResourceName, bool isNetWarningExpected, string[]? formatArgs = null)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = @$"
                <Project DefaultTargets=""Build"">
                    <Import Project=""$(MSBuildBinPath)\Microsoft.Common.props"" />

                    <PropertyGroup>
                        <Platform>{(isPlatformAnyCpu ? "AnyCPU" : "Release")}</Platform>
                        <OutputType>Library</OutputType>
                        <PreferNativeArm64>true</PreferNativeArm64>
                        <Prefer32Bit>{(prefer32 ? "true" : "false")}</Prefer32Bit>
                    </PropertyGroup>

                    <Target Name=""Build""/>
                    <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />

                </Project>
                ";

                var projectFile = env.CreateFile(env.CreateFolder(), "test.csproj", projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName, formatArgs);
                MockLogger logger = new MockLogger(_testOutput);

                project.Build(logger);

                if (isNetWarningExpected)
                {
                    logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
                else
                {
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
            }
        }

        [Theory]
        [InlineData(true, "CommonSdk.BaseIntermediateOutputPathMismatchWarning")]
        [InlineData(false, "CommonSdk.MSBuildProjectExtensionsPathModifiedAfterUse")]

        public void BaseIntermediateOutputPathMisMatchWarning(bool isInitialMSBuildProjectExtensionsPathEmpty, string expectedResourceName)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = $"""
                <Project>
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <PropertyGroup>
                        <EnableBaseIntermediateOutputPathMismatchWarning>true</EnableBaseIntermediateOutputPathMismatchWarning>
                        <_InitialMSBuildProjectExtensionsPath>{(isInitialMSBuildProjectExtensionsPathEmpty ? "" : "obj")}</_InitialMSBuildProjectExtensionsPath>
                        <MSBuildProjectExtensionsPath></MSBuildProjectExtensionsPath>
                        <BaseIntermediateOutputPath>obj\Debug\</BaseIntermediateOutputPath>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
                </Project>
                """;

                var projectFile = env.CreateFile(env.CreateFolder(), "test.csproj", projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName);
                MockLogger logger = new MockLogger(_testOutput);

                project.Build(logger);
                if (!isInitialMSBuildProjectExtensionsPathEmpty)
                {
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
                else
                {
                    logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
            }
        }

        [Theory]
        [InlineData("SetGenerateManifests", "CommonSdk.GenerateManifestsOnlyForExe", false)]
        [InlineData("SetGenerateManifests", "CommonSdk.SigningKeyRequired", true)]
        [InlineData("_DeploymentUnpublishable", "CommonSdk.DeploymentUnpublishable")]
        [InlineData("Run", "CommonSdk.RunTargetDependsOnMessage")]
        [InlineData("GetTargetFrameworks", "CommonSdk.CrossTargetingGetTargetFrameworks")]
        [InlineData("ResolveProjectReferences", "CommonSdk.NonExistentProjectReference")]
        [InlineData("ResolveProjectReferences", "CommonSdk.NonExistentProjectReference", true, false)]
        public void RunTargetExtError(string targetName, string expectedResourceName, bool outputTypeIsExe = true, bool errorOnMissingProjectReference = true)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = $"""
                <Project DefaultTargets="{targetName}">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <PropertyGroup>
                        <TargetExt>.txt</TargetExt>
                        <OutputPath>bin</OutputPath>
                        <OutputType>{(outputTypeIsExe ? "" : "txt")}</OutputType>
                        <_DeploymentSignClickOnceManifests>true</_DeploymentSignClickOnceManifests>
                        <ManifestCertificateThumbprint></ManifestCertificateThumbprint>
                        <ManifestKeyFile></ManifestKeyFile>
                        <TargetFrameworks>netcoreapp3.1;net6.0;net7.0</TargetFrameworks>
                        <ErrorOnMissingProjectReference>{errorOnMissingProjectReference}</ErrorOnMissingProjectReference>
                    </PropertyGroup>

                    <ItemGroup>
                        <ProjectReference Include="NonExistent.csproj" />
                    </ItemGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets"/>

                </Project>
                """;

                var projectFile = env.CreateFile(env.CreateFolder(), "test.csproj", projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                MockLogger logger = new MockLogger(_testOutput);

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName);

                project.Build(logger);
                if (expectedResourceName == "CommonSdk.DeploymentUnpublishable")
                {
                    logger.FullLog.Contains(expectedBuildMessage);
                }
                else if (expectedResourceName == "CommonSdk.RunTargetDependsOnMessage")
                {
                    var targetPathParameter = expectedResourceName == "CommonSdk.DeploymentUnpublishable" ? "" : Path.Combine(project.DirectoryPath, "bin", "test.txt");
                    expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName, targetPathParameter);
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
                else if (expectedResourceName == "CommonSdk.NonExistentProjectReference")
                {
                    expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName, "NonExistent.csproj");
                    if (errorOnMissingProjectReference)
                    {
                        logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                    }
                    else
                    {
                        logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
                    }
                }
                else
                {
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
            }
        }

        /// <summary>
        /// SkipInvalidConfigurations is true, the output is warning, otherwise is error.
        /// BuildingInsideVisualStudio is true, the resourceName is CommonSdk.InvalidConfigurationTextWhenBuildingInsideVisualStudio, otherwise is CommonSdk.InvalidConfigurationTextWhenBuildingOutsideVisualStudio
        /// </summary>
        /// <param name="expectedResourceName"></param>
        /// <param name="skipInvalidConfigurations"></param>
        /// <param name="buildingInsideVisualStudio"></param>
        [Theory]
        [InlineData("CommonSdk.InvalidConfigurationTextWhenBuildingInsideVisualStudio", false, true)]
        [InlineData("CommonSdk.InvalidConfigurationTextWhenBuildingOutsideVisualStudio", true, false)]
        [InlineData("CommonSdk.InvalidConfigurationTextWhenBuildingOutsideVisualStudio", false, false)]
        [InlineData("CommonSdk.InvalidConfigurationTextWhenBuildingInsideVisualStudio", true, true)]
        public void CheckForInvalidConfigurationAndPlatformTargetMessage(string expectedResourceName, bool skipInvalidConfigurations, bool buildingInsideVisualStudio)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                var fileName = "test.csproj";
                var configuration = "Release";
                var platform = "Release";
                string projectContent = $"""
                <Project DefaultTargets="Build">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <PropertyGroup>
                         <SkipInvalidConfigurations>{skipInvalidConfigurations}</SkipInvalidConfigurations>
                         <BuildingInsideVisualStudio>{buildingInsideVisualStudio}</BuildingInsideVisualStudio>
                         <BaseOutputPathWasSpecified>false</BaseOutputPathWasSpecified>
                         <_OutputPathWasMissing>true</_OutputPathWasMissing>
                         <Configuration>{configuration}</Configuration>
                         <Platform>{platform}</Platform>
                    </PropertyGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets"/>

                </Project>
                """;

                var projectFile = env.CreateFile(env.CreateFolder(), fileName, projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                MockLogger logger = new MockLogger(_testOutput);

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName, fileName, configuration, platform);

                project.Build(logger);
                if (skipInvalidConfigurations)
                {
                    logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
                else
                {
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
            }
        }

        [Theory]
        [InlineData("MSB9000", "ResxWithNoCulture", "SplitResourcesByCulture", "CommonSdk.SplitResourcesByCultureEmbeddedResourceMessage"),]
        [InlineData("MSB9001", "ResxWithCulture", "SplitResourcesByCulture", "CommonSdk.SplitResourcesByCultureEmbeddedResourceMessage")]
        [InlineData("MSB9002", "NonResxWithCulture", "SplitResourcesByCulture", "CommonSdk.SplitResourcesByCultureEmbeddedResourceMessage")]
        [InlineData("MSB9003", "NonResxWithNoCulture", "SplitResourcesByCulture", "CommonSdk.SplitResourcesByCultureEmbeddedResourceMessage")]
        [InlineData("MSB9004", "ManifestResourceWithNoCulture", "_GenerateCompileInputs", "CommonSdk.ManifestResourceWithNoCultureWarning")]
        [InlineData("MSB9005", "ManifestNonResxWithNoCultureOnDisk", "_GenerateCompileInputs", "CommonSdk.ManifestResourceWithNoCultureWarning")]
        [InlineData("MSB9006", "ManifestResourceWithCulture", "_GenerateSatelliteAssemblyInputs", "CommonSdk.ManifestResourceWithNoCultureWarning")]
        [InlineData("MSB9007", "ManifestNonResxWithCultureOnDisk", "_GenerateSatelliteAssemblyInputs", "CommonSdk.ManifestResourceWithNoCultureWarning")]
        public void ResourcesByCultureWarningMessage(string warningNumber, string itemName, string targetName, string resourceName)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = $"""
                <Project DefaultTargets="{targetName}">
                    <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />

                    <ItemGroup>
                        <{itemName} Include="Value1" />
                    </ItemGroup>

                    <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets"/>
                </Project>
                """;

                var projectFile = env.CreateFile(env.CreateFolder(), "test.csproj", projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                MockLogger logger = new MockLogger(_testOutput);
                object[] args = [warningNumber, itemName];
                if (warningNumber == "MSB9004")
                {
                    args = [warningNumber, itemName, "false", "Resx"];
                }
                else if (warningNumber == "MSB9005")
                {
                    args = [warningNumber, itemName, "false", "Non-Resx"];
                }
                else if (warningNumber == "MSB9006")
                {
                    args = [warningNumber, itemName, "true", "Resx"];
                }
                else if (warningNumber == "MSB9007")
                {
                    args = [warningNumber, itemName, "true", "Non-Resx"];
                }

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, args);

                project.Build(logger);
                logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
            }
        }
    }
}
