// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tests
{
    public class TestSetupFixture : TestBase
    {
        private const string Framework = "net46";
        private const string Config = "Debug";
        private const string AppWithConfig = "AppWithRedirectsAndConfig";
        private const string AppWithoutConfig = "AppWithRedirectsNoConfig";

        private string _Runtime = RuntimeEnvironmentRidExtensions.GetLegacyRestoreRuntimeIdentifier();
        private string _desktopProjectsRoot = Path.Combine(RepoRoot, "TestAssets", "DesktopTestProjects");
        private string _buildRelativePath;
        private string _appWithConfigProjectRoot;
        private string _appWithConfigBuildDir;
        private string _appWithConfigPublishDir;
        private string _appWithoutConfigProjectRoot;
        private string _appWithoutConfigBuildDir;
        private string _appWithoutConfigPublishDir;
        private TestInstance _testInstance;

        public string AppWithConfigProjectRoot { get { return _appWithConfigProjectRoot; } }
        public string AppWithConfigBuildOutput { get; }
        public string AppWithConfigPublishOutput { get; }
        public string AppWithoutConfigProjectRoot { get { return _appWithoutConfigProjectRoot; } }
        public string AppWithoutConfigBuildOutput { get; }
        public string AppWithoutConfigPublishOutput { get; }

        public TestSetupFixture()
        {
            _buildRelativePath = Path.Combine("bin", Config, Framework, _Runtime);
            var testAssetsMgr = new TestAssetsManager(_desktopProjectsRoot);
            _testInstance = testAssetsMgr.CreateTestInstance("BindingRedirectSample")
                                         .WithLockFiles();

            Setup(AppWithConfig, ref _appWithConfigProjectRoot, ref _appWithConfigBuildDir, ref _appWithConfigPublishDir);
            Setup(AppWithoutConfig, ref _appWithoutConfigProjectRoot, ref _appWithoutConfigBuildDir, ref _appWithoutConfigPublishDir);

            AppWithConfigBuildOutput = Path.Combine(_appWithConfigBuildDir, AppWithConfig + ".exe");
            AppWithConfigPublishOutput = Path.Combine(_appWithConfigPublishDir, AppWithConfig + ".exe");
            AppWithoutConfigBuildOutput = Path.Combine(_appWithoutConfigBuildDir, AppWithoutConfig + ".exe");
            AppWithoutConfigPublishOutput = Path.Combine(_appWithoutConfigPublishDir, AppWithoutConfig + ".exe");
        }

        private void Setup(string project, ref string projectDir, ref string buildDir, ref string publishDir)
        {
            projectDir = Path.Combine(_testInstance.TestRoot, project);
            buildDir = Path.Combine(projectDir, _buildRelativePath);
            publishDir = Path.Combine(projectDir, "publish");

            var buildCommand = new BuildCommand(projectDir, framework: Framework, runtime: _Runtime);
            buildCommand.Execute().Should().Pass();

            var publishCommand = new PublishCommand(projectDir, output: publishDir, framework: Framework, runtime: _Runtime);
            publishCommand.Execute().Should().Pass();
        }
    }
}
