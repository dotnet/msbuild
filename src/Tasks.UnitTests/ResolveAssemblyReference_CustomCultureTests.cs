// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public class ResolveAssemblyReference_CustomCultureTests
    {
        private static string TestAssetsRootPath { get; } = Path.Combine(
            Path.GetDirectoryName(typeof(AddToWin32Manifest_Tests).Assembly.Location) ?? AppContext.BaseDirectory,
            "TestResources",
            "CustomCulture");

        [WindowsOnlyTheory]
        [InlineData(true, "", true, true)]
        [InlineData(false)]
        [InlineData(true, "yue", false, true)]
        [InlineData(false, "yue", false, true)]
        [InlineData(true, "euy", true)]
        [InlineData(true, "yue;euy")]
        [InlineData(true, "euy;yue")]
        public void E2EScenarioTests(
            bool enableCustomCulture,
            string customCultureExclusions = "",
            bool isYueCultureExpected = false,
            bool isEuyCultureExpected = false)
        {
            // Skip test if running in .NET Core SDK (relevant for VS .NetFramework only)
            var extensionsPath = Environment.GetEnvironmentVariable("MSBuildExtensionsPath");
            if (!string.IsNullOrEmpty(extensionsPath) && extensionsPath.Contains(Path.Combine("core", "sdk")))
            {
                return;
            }

            using (TestEnvironment env = TestEnvironment.Create())
            {
                try
                {
                    // Configure environment
                    env.SetEnvironmentVariable("MSBUILDENABLECUSTOMCULTURES", enableCustomCulture ? "1" : "");

                    // Set up project structure
                    var testAssetsPath = TestAssetsRootPath;
                    var solutionFolder = env.CreateFolder();
                    var solutionPath = solutionFolder.Path;
                    var outputFolder = env.CreateFolder();
                    var projBOutputPath = outputFolder.Path;

                    SetupProjectB(env, testAssetsPath, solutionPath, projBOutputPath, customCultureExclusions);

                    env.SetCurrentDirectory(Path.Combine(solutionPath, "ProjectB.csproj"));
                    string output = RunnerUtilities.ExecBootstrapedMSBuild("-restore", out bool buildSucceeded);
                    buildSucceeded.ShouldBeTrue($"MSBuild should complete successfully. Build output: {output}");

                    VerifyCustomCulture(enableCustomCulture, isYueCultureExpected, "yue", projBOutputPath);
                    VerifyCustomCulture(enableCustomCulture, isEuyCultureExpected, "euy", projBOutputPath);
                }
                finally
                {
                    env.SetEnvironmentVariable("MSBUILDENABLECUSTOMCULTURES", "");
                }
            }
        }

        private void SetupProjectB(TestEnvironment env, string testAssetsPath, string solutionPath,
            string projBOutputPath, string customCultureExclusions)
        {
            var projectBName = "ProjectB.csproj";
            var projectBFolder = Path.Combine(solutionPath, projectBName);
            Directory.CreateDirectory(projectBFolder);

            var projBContent = File.ReadAllText(Path.Combine(testAssetsPath, projectBName))
                .Replace("OutputPathPlaceholder", projBOutputPath)
                .Replace("NonCultureResourceDirectoriesPlaceholder", customCultureExclusions);

            env.CreateFile(Path.Combine(projectBFolder, projectBName), projBContent);

            CopyProjectAssets(testAssetsPath, solutionPath);
        }

        private void CopyProjectAssets(string testAssetsPath, string solutionPath)
        {
            CopyTestAsset(testAssetsPath, "ProjectA.csproj", solutionPath);
            CopyTestAsset(testAssetsPath, "Test.resx", solutionPath);
            CopyTestAsset(testAssetsPath, "Test.yue.resx", solutionPath);
            CopyTestAsset(testAssetsPath, "Test.euy.resx", solutionPath);
        }

        private void VerifyCustomCulture(bool enableCustomCulture, bool isCultureExpectedToExist,
            string customCultureName, string outputPath)
        {
            var cultureResourcePath = Path.Combine(outputPath, customCultureName, "ProjectA.resources.dll");

            if (enableCustomCulture && isCultureExpectedToExist)
            {
                File.Exists(cultureResourcePath).ShouldBeTrue(
                    $"Expected '{customCultureName}' resource DLL not found at: {cultureResourcePath}");
            }
            else
            {
                File.Exists(cultureResourcePath).ShouldBeFalse(
                    $"Unexpected '{customCultureName}' culture DLL was found at: {cultureResourcePath}");
            }
        }

        private void CopyTestAsset(string sourceFolder, string fileName, string destinationFolder)
        {
            var sourcePath = Path.Combine(sourceFolder, fileName);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Test asset not found: {sourcePath}");
            }

            File.Copy(sourcePath, Path.Combine(destinationFolder, fileName));
        }
    }
}
