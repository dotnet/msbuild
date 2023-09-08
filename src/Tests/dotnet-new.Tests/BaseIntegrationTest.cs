// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public abstract class BaseIntegrationTest : SdkTest
    {
        public BaseIntegrationTest(ITestOutputHelper log) : base(log)
        {
        }

        /// <summary>
        /// Gets a path to the folder with dotnet new test assets.
        /// </summary>
        public static string DotnetNewTestAssets { get; } = VerifyExists(Path.Combine(TestContext.Current.TestAssetsDirectory, "TestPackages", "dotnet-new"));

        /// <summary>
        /// Gets a path to the folder with dotnet new test NuGet template packages.
        /// </summary>
        public static string DotnetNewTestPackagesBasePath { get; } = VerifyExists(Path.Combine(DotnetNewTestAssets, "nupkg_templates"));

        /// <summary>
        /// Gets a path to the folder with dotnet new test templates.
        /// </summary>
        public static string DotnetNewTestTemplatesBasePath { get; } = VerifyExists(Path.Combine(DotnetNewTestAssets, "test_templates"));

        /// <summary>
        /// Gets a path to the folder with dotnet new test templates.
        /// </summary>
        public static string DotnetNewTestTemplatePackageProjectPath { get; } = VerifyFileExists(Path.Combine(DotnetNewTestAssets, "Microsoft.TemplateEngine.TestTemplates.csproj"));

        /// <summary>
        /// Gets a path to the repo root folder.
        /// </summary>
        public static string CodeBaseRoot { get; } = GetAndVerifyRepoRoot();

        /// <summary>
        /// Gets a path to the template packages maintained in the repo (/template_feed).
        /// </summary>
        public static string RepoTemplatePackages { get; } = VerifyExists(Path.Combine(CodeBaseRoot, "template_feed"));

#if DEBUG
        /// <summary>
        /// Gets configuration name.
        /// </summary>
        public static string Configuration { get; } = "Debug";
#else
        /// <summary>
        /// Gets configuration name.
        /// </summary>
        public static string Configuration { get; } = "Release";
#endif

        /// <summary>
        /// Gets a path to the test template with a <paramref name="templateName"/> name.
        /// </summary>
        public static string GetTestTemplateLocation(string templateName)
        {
            string templateLocation = Path.GetFullPath(Path.Combine(DotnetNewTestTemplatesBasePath, templateName));
            if (!Directory.Exists(templateLocation))
            {
                Assert.False(true, $"The test template '{templateName}' does not exist.");
            }
            return templateLocation;
        }

        /// <summary>
        /// Creates a temp test directory under test execution folder.
        /// Format: artifacts\tmp\Debug\dotnet-new.IntegrationTests\<paramref name="testName"/>\<paramref name="folderName"/>\date-time-utc-now[optional counter].
        /// </summary>
        /// <remarks>
        /// Use this method when temp folder should be under location that is aware of repo nuget.config.
        /// This is required for example for restore for the latest framework.
        /// Otherwise <see cref="TemplateEngine.TestHelper.TestUtils.CreateTemporaryFolder"/> can also be used.
        /// </remarks>
        public static string CreateTemporaryFolder([CallerMemberName] string testName = "UnnamedTest", string folderName = "")
        {
            return Utilities.CreateTemporaryFolder(testName, folderName);
        }

        /// <summary>
        /// Installs <paramref name="packageName"/> to dotnet new.
        /// </summary>
        /// <param name="packageName">The package to install.</param>
        /// <param name="log">Test logger.</param>
        /// <param name="homeDirectory">The settings path for dotnet new.</param>
        /// <param name="workingDirectory">The working directory to use.</param>
        internal static void InstallNuGetTemplate(string packageName, ITestOutputHelper log, string homeDirectory, string? workingDirectory = null)
        {
            DotnetNewCommand command = new DotnetNewCommand(log, "-i", packageName)
                  .WithCustomHive(homeDirectory);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                command.WithWorkingDirectory(workingDirectory);
            }

            command.Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
        }

        /// <summary>
        /// Installs test template to dotnet new.
        /// </summary>
        /// <param name="templateNameOrPath">The name or path of the test tempalte to install.</param>
        /// <param name="log">Test logger.</param>
        /// <param name="homeDirectory">The settings path for dotnet new.</param>
        /// <param name="workingDirectory">The working directory to use.</param>
        internal string InstallTestTemplate(string templateNameOrPath, ITestOutputHelper log, string homeDirectory, string? workingDirectory = null)
        {
            string testTemplate = GetTestTemplateLocation(templateNameOrPath);

            if (Directory.Exists(templateNameOrPath))
            {
                testTemplate = templateNameOrPath;
            }

            DotnetNewCommand command = new DotnetNewCommand(log, "install", testTemplate)
                .WithCustomHive(homeDirectory);

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                command.WithWorkingDirectory(workingDirectory);
            }

            command.Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
            return Path.GetFullPath(testTemplate);
        }

        /// <summary>
        /// Packs test template package and returns path to it.
        /// </summary>
        internal string PackTestNuGetPackage(ITestOutputHelper log, [CallerMemberName] string testName = "UnnamedTest")
        {
            var testAsset = _testAssetsManager.CopyTestAsset("dotnet-new", callingMethod: testName, testAssetSubdirectory: "TestPackages").WithSource();
            string testProject = Path.GetFileName(DotnetNewTestTemplatePackageProjectPath);
            string testPath = testAsset.Path;

            string outputLocation = Path.Combine(testPath, "TestNuGetPackage");

            new DotnetPackCommand(log, $"{testPath}\\{testProject}", "-o", outputLocation)
                .Execute()
                .Should()
            .Pass();

            string createdPackagePath = Directory.GetFiles(outputLocation).Single(f => Path.GetExtension(f).Equals(".nupkg", StringComparison.OrdinalIgnoreCase));

            return createdPackagePath;
        }

        private static string VerifyExists(string folder)
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
            {
                Assert.False(true, $"The folder '{folder}' does not exist.");
            }
            return folder;
        }

        private static string VerifyFileExists(string file)
        {
            file = Path.GetFullPath(file);
            if (!File.Exists(file))
            {
                Assert.False(true, $"The file '{file}' does not exist.");
            }
            return file;
        }

        private static string GetAndVerifyRepoRoot()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(TestContext.Current.TestAssetsDirectory, "..", ".."));
            if (!Directory.Exists(repoRoot))
            {
                Assert.False(true, $"The repo root cannot be evaluated.");
            }
            if (!File.Exists(Path.Combine(repoRoot, "sdk.sln")))
            {
                Assert.False(true, $"The repo root doesn't contain 'sdk.sln'.");
            }
            return repoRoot;
        }
    }
}
