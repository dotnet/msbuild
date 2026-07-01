// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public class ResolveAssemblyReference_CustomCultureTests
    {
        private readonly ITestOutputHelper _output;

        public ResolveAssemblyReference_CustomCultureTests(ITestOutputHelper output)
        {
            _output = output;
        }

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
        public void E2EScenarioTests(bool enableCustomCulture, string customCultureExclusions = "", bool isYueCultureExpected = false, bool isEuyCultureExpected = false)
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                // Set up project paths
                var testAssetsPath = TestAssetsRootPath;
                var solutionFolder = env.CreateFolder();
                var solutionPath = solutionFolder.Path;

                // Create and configure ProjectB
                var projectBName = "ProjectB.csproj";
                var projBOutputPath = env.CreateFolder().Path;
                var projectBFolder = Path.Combine(solutionPath, projectBName);
                Directory.CreateDirectory(projectBFolder);
                var projBContent = File.ReadAllText(Path.Combine(testAssetsPath, projectBName))
                    .Replace("OutputPathPlaceholder", projBOutputPath)
                    .Replace("NonCultureResourceDirectoriesPlaceholder", customCultureExclusions)
                    .Replace("EnableCustomCulturePlaceholder", enableCustomCulture.ToString());
                env.CreateFile(Path.Combine(projectBFolder, projectBName), projBContent);

                // Copy ProjectA files to test solution folder
                CopyTestAsset(testAssetsPath, "ProjectA.csproj", solutionPath);
                CopyTestAsset(testAssetsPath, "Test.resx", solutionPath);
                CopyTestAsset(testAssetsPath, "Test.yue.resx", solutionPath);
                CopyTestAsset(testAssetsPath, "Test.euy.resx", solutionPath);

                env.SetCurrentDirectory(projectBFolder);
                var output = RunnerUtilities.ExecBootstrapedMSBuild("-restore", out bool buildSucceeded);

                if (!buildSucceeded)
                {
                    _output.WriteLine(output);
                }

                buildSucceeded.ShouldBeTrue("MSBuild should complete successfully.");

                var yueCultureResourceDll = Path.Combine(projBOutputPath, "yue", "ProjectA.resources.dll");
                AssertCustomCulture(isYueCultureExpected, "yue", yueCultureResourceDll);

                var euyCultureResourceDll = Path.Combine(projBOutputPath, "euy", "ProjectA.resources.dll");
                AssertCustomCulture(isEuyCultureExpected, "euy", euyCultureResourceDll);
            }

            void AssertCustomCulture(bool isCultureExpectedToExist, string customCultureName, string cultureResourcePath)
            {
                if (enableCustomCulture && isCultureExpectedToExist)
                {
                    File.Exists(cultureResourcePath).ShouldBeTrue($"Expected '{customCultureName}' resource DLL not found at: {cultureResourcePath}");
                }
                else
                {
                    File.Exists(cultureResourcePath).ShouldBeFalse($"Unexpected '{customCultureName}' culture DLL was found at: {cultureResourcePath}");
                }
            }
        }

        private void CopyTestAsset(string sourceFolder, string fileName, string destinationFolder)
        {
            var sourcePath = Path.Combine(sourceFolder, fileName);

            File.Copy(sourcePath, Path.Combine(destinationFolder, fileName));
        }
    }
}
