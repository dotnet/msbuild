// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetRestorePostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetRestorePostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(DotnetRestoreCanTargetASingleProjectWithAJsonArray))]
        public void DotnetRestoreCanTargetASingleProjectWithAJsonArray()
        {
            var callback = new MockDotnetRestoreCallback();
            DotnetRestorePostActionProcessor actionProcessor = new(callback.RestoreProject);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "files", "[\"MyApp.csproj\"]" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = actionProcessor.Id, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

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
            var callback = new MockDotnetRestoreCallback();
            DotnetRestorePostActionProcessor actionProcessor = new(callback.RestoreProject);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "files", "MyApp.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = actionProcessor.Id, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

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
                Target = target;
                return true;
            }
        }
    }
}
