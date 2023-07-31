// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetSlnPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetSlnPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindSolutionFileAtOutputPath))]
        public void AddProjectToSolutionPostActionFindSolutionFileAtOutputPath()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string solutionFileFullPath = Path.Combine(targetBasePath, "MySln.sln");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(solutionFileFullPath, string.Empty);

            IReadOnlyList<string> solutionFiles = DotnetSlnPostActionProcessor.FindSolutionFilesAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, targetBasePath);
            Assert.Equal(1, solutionFiles.Count);
            Assert.Equal(solutionFileFullPath, solutionFiles[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsOneProjectToAdd))]
        public void AddProjectToSolutionPostActionFindsOneProjectToAdd()
        {
            string outputBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath(Path.GetFullPath("outputProj1.csproj")) });

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.Equal(1, foundProjectFiles?.Count);
            Assert.Equal(creationResult.PrimaryOutputs[0].Path, foundProjectFiles?[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAdd))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAdd()
        {
            string outputBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0; 2" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath(Path.GetFullPath("outputProj1.csproj")),
                    new MockCreationPath(Path.GetFullPath("dontFindMe.csproj")),
                    new MockCreationPath(Path.GetFullPath("outputProj2.csproj"))
                });

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(creationResult.PrimaryOutputs[0].Path, foundProjectFiles.ToList());
            Assert.Contains(creationResult.PrimaryOutputs[2].Path, foundProjectFiles.ToList());

            Assert.DoesNotContain(creationResult.PrimaryOutputs[1].Path, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionDoesntFindProjectOutOfRange))]
        public void AddProjectToSolutionPostActionDoesntFindProjectOutOfRange()
        {
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "1" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath("outputProj1.csproj") });

            Assert.False(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, string.Empty, out IReadOnlyList<string>? foundProjectFiles));
            Assert.Null(foundProjectFiles);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath()
        {
            string outputBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
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

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath2, foundProjectFiles.ToList());

            Assert.DoesNotContain(dontFindMeFullPath1, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath))]
        public void AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath()
        {
            string outputBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("outputProj2.csproj"),
                });
            string outputFileFullPath0 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[0].Path);
            string outputFileFullPath1 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[1].Path);

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath1, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionCanTargetASingleProjectWithAJsonArray))]
        public void AddProjectToSolutionCanTargetASingleProjectWithAJsonArray()
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string slnFileFullPath = Path.Combine(targetBasePath, "MyApp.sln");
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() { { "projectFiles", "[\"MyApp.csproj\"]" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(new[] { projFileFullPath }, callback.Projects);
            Assert.Equal(slnFileFullPath, callback.Solution);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionCanTargetASingleProjectWithTheProjectName))]
        public void AddProjectToSolutionCanTargetASingleProjectWithTheProjectName()
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string slnFileFullPath = Path.Combine(targetBasePath, "MyApp.sln");
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() { { "projectFiles", "MyApp.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(new[] { projFileFullPath }, callback.Projects);
            Assert.Equal(slnFileFullPath, callback.Solution);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionCanPlaceProjectInSolutionRoot))]
        public void AddProjectToSolutionCanPlaceProjectInSolutionRoot()
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string slnFileFullPath = Path.Combine(targetBasePath, "MyApp.sln");
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "inRoot", "true" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.True(callback.InRoot);
            Assert.Null(callback.TargetFolder);
        }

        [Fact]
        public void AddProjectToSolutionCanPlaceProjectInSolutionFolder()
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string slnFileFullPath = Path.Combine(targetBasePath, "MyApp.sln");
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "solutionFolder", "src" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Null(callback.InRoot);
            Assert.Equal("src", callback.TargetFolder);
        }

        [Fact]
        public void AddProjectToSolutionFailsWhenSolutionFolderAndInRootSpecified()
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string slnFileFullPath = Path.Combine(targetBasePath, "MyApp.sln");
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "inRoot", "true" },
                { "solutionFolder", "src" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            bool result = actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);
        }

        private class MockAddProjectToSolutionCallback
        {
            public string? Solution { get; private set; }

            public IReadOnlyList<string?>? Projects { get; private set; }

            public string? TargetFolder { get; private set; }

            public bool? InRoot { get; private set; }

            public bool AddProjectToSolution(string solution, IReadOnlyList<string?> projects, string? targetFolder, bool? inRoot)
            {
                Solution = solution;
                Projects = projects;
                InRoot = inRoot;
                TargetFolder = targetFolder;

                return true;
            }
        }
    }
}
