// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class DotnetRestorePostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetRestorePostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(DotnetRestoreCanTargetASingleProjectWithAJsonArray))]
        public void DotnetRestoreCanTargetASingleProjectWithAJsonArray()
        {
            DotnetRestorePostActionProcessor actionProcessor = new DotnetRestorePostActionProcessor();

            string targetBasePath = _engineEnvironmentSettings.GetNewVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "files", "[\"MyApp.csproj\"]" } };
            var postAction = new MockPostAction { ActionId = actionProcessor.Id, Args = args };

            var creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            var callback = new MockDotnetRestoreCallback();
            actionProcessor.Callbacks = new NewCommandCallbacks { RestoreProject = callback.RestoreProject };

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
        }

        [Fact(DisplayName = nameof(DotnetRestoreCanTargetASingleProjectWithTheProjectName))]
        public void DotnetRestoreCanTargetASingleProjectWithTheProjectName()
        {
            DotnetRestorePostActionProcessor actionProcessor = new DotnetRestorePostActionProcessor();

            string targetBasePath = _engineEnvironmentSettings.GetNewVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "files", "MyApp.csproj" } };
            var postAction = new MockPostAction { ActionId = actionProcessor.Id, Args = args };

            var creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            var callback = new MockDotnetRestoreCallback();
            actionProcessor.Callbacks = new NewCommandCallbacks { RestoreProject = callback.RestoreProject };

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
        }

        private class MockDotnetRestoreCallback
        {
            public string? Target { get; private set; }

            public bool RestoreProject(string target)
            {
                this.Target = target;

                return true;
            }
        }
    }
}
