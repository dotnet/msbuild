// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class AddProjectsToSolutionPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public AddProjectsToSolutionPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindSolutionFileAtOutputPath))]
        public void AddProjectToSolutionPostActionFindSolutionFileAtOutputPath()
        {
            string targetBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);
            string solutionFileFullPath = Path.Combine(targetBasePath, "MySln.sln");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(solutionFileFullPath, string.Empty);

            IReadOnlyList<string> solutionFiles = AddProjectsToSolutionPostAction.FindSolutionFilesAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, targetBasePath);
            Assert.Equal(1, solutionFiles.Count);
            Assert.Equal(solutionFileFullPath, solutionFiles[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsOneProjectToAdd))]
        public void AddProjectToSolutionPostActionFindsOneProjectToAdd()
        {
            IPostAction postAction = new MockPostAction()
            {
                ActionId = AddProjectsToSolutionPostAction.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath("outputProj1.csproj") });

            Assert.True(AddProjectsToSolutionPostAction.TryGetProjectFilesToAdd(_engineEnvironmentSettings, postAction, creationResult, string.Empty, out IReadOnlyList<string> foundProjectFiles));
            Assert.Equal(1, foundProjectFiles.Count);
            Assert.Equal(creationResult.PrimaryOutputs[0].Path, foundProjectFiles[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAdd))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAdd()
        {
            IPostAction postAction = new MockPostAction()
            {
                ActionId = AddProjectsToSolutionPostAction.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0; 2" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("dontFindMe.csproj"),
                    new MockCreationPath("outputProj2.csproj")
                });

            Assert.True(AddProjectsToSolutionPostAction.TryGetProjectFilesToAdd(_engineEnvironmentSettings, postAction, creationResult, string.Empty, out IReadOnlyList<string> foundProjectFiles));
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(creationResult.PrimaryOutputs[0].Path, foundProjectFiles.ToList());
            Assert.Contains(creationResult.PrimaryOutputs[2].Path, foundProjectFiles.ToList());

            Assert.DoesNotContain(creationResult.PrimaryOutputs[1].Path, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionDoesntFindProjectOutOfRange))]
        public void AddProjectToSolutionPostActionDoesntFindProjectOutOfRange()
        {
            IPostAction postAction = new MockPostAction()
            {
                ActionId = AddProjectsToSolutionPostAction.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "1" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath("outputProj1.csproj") });

            Assert.False(AddProjectsToSolutionPostAction.TryGetProjectFilesToAdd(_engineEnvironmentSettings, postAction, creationResult, string.Empty, out IReadOnlyList<string> foundProjectFiles));
            Assert.Null(foundProjectFiles);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath()
        {
            string outputBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            IPostAction postAction = new MockPostAction()
            {
                ActionId = AddProjectsToSolutionPostAction.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0; 2" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("dontFindMe.csproj"),
                    new MockCreationPath("outputProj2.csproj")
                });
            string outputFileFullPath0 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[0].Path);
            string dontFindMeFullPath1 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[1].Path);
            string outputFileFullPath2 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[2].Path);

            Assert.True(AddProjectsToSolutionPostAction.TryGetProjectFilesToAdd(_engineEnvironmentSettings, postAction, creationResult, outputBasePath, out IReadOnlyList<string> foundProjectFiles));
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath2, foundProjectFiles.ToList());

            Assert.DoesNotContain(dontFindMeFullPath1, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath))]
        public void AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath()
        {
            string outputBasePath = FileSystemHelpers.GetNewVirtualizedPath(_engineEnvironmentSettings);

            IPostAction postAction = new MockPostAction()
            {
                ActionId = AddProjectsToSolutionPostAction.ActionProcessorId,
                Args = new Dictionary<string, string>()
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new []
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("outputProj2.csproj"),
                });
            string outputFileFullPath0 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[0].Path);
            string outputFileFullPath1 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[1].Path);

            Assert.True(AddProjectsToSolutionPostAction.TryGetProjectFilesToAdd(_engineEnvironmentSettings, postAction, creationResult, outputBasePath, out IReadOnlyList<string> foundProjectFiles));
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath1, foundProjectFiles.ToList());
        }
    }
}
