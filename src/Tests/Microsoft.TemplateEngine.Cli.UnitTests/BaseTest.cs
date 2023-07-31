// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public abstract class BaseTest
    {
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
        /// Gets a path to the repo root folder.
        /// </summary>
        public static string CodeBaseRoot { get; } = GetAndVerifyRepoRoot();

        /// <summary>
        /// Gets a path to the template packages maintained in the repo (/template_feed).
        /// </summary>
        public static string RepoTemplatePackages { get; } = VerifyExists(Path.Combine(CodeBaseRoot, "template_feed"));

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

        private static string VerifyExists(string folder)
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
            {
                Assert.False(true, $"The folder '{folder}' does not exist.");
            }
            return folder;
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
