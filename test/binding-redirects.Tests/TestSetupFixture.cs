// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;

namespace Microsoft.DotNet.BindingRedirects.Tests
{
    public class TestSetupFixture : TestBase
    {
        private readonly NuGetFramework Framework = NuGet.Frameworks.FrameworkConstants.CommonFrameworks.Net46;
        private const string Config = "Debug";
        private const string AppWithConfig = "AppWithRedirectsAndConfig";
        private const string AppWithoutConfig = "AppWithRedirectsNoConfig";

        private string _Runtime = RuntimeEnvironmentRidExtensions.GetLegacyRestoreRuntimeIdentifier();
        private string _appWithConfigProjectRoot;
        private string _appWithoutConfigProjectRoot;
        private TestAssetInstance _testInstance;

        public string AppWithConfigProjectRoot { get { return _appWithConfigProjectRoot; } }
        public string AppWithoutConfigProjectRoot { get { return _appWithoutConfigProjectRoot; } }

        public TestSetupFixture()
        {
            _testInstance = TestAssets.Get("DesktopTestProjects", "BindingRedirectSample")
                .CreateInstance()
                .WithSourceFiles()
                .WithNuGetConfig(new RepoDirectoriesProvider().TestPackages);

            _appWithConfigProjectRoot = Setup(AppWithConfig);
            _appWithoutConfigProjectRoot = Setup(AppWithoutConfig);
        }

        private string Setup(string project)
        {
            string projectDir = Path.Combine(_testInstance.Root.FullName, project);
            string publishDir = Path.Combine(projectDir, "publish");

            new RestoreCommand()
                .WithWorkingDirectory(projectDir)
                .WithRuntime(_Runtime)
                .ExecuteWithCapturedOutput()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDir)
                .WithFramework(Framework)
                .WithRuntime(_Runtime)
                .Execute()
                .Should().Pass();

            new PublishCommand()
                .WithWorkingDirectory(projectDir)
                .WithOutput(publishDir)
                .WithFramework(Framework)
                .WithRuntime(_Runtime)
                .Execute()
                .Should().Pass();

            return projectDir;
        }
    }
}
