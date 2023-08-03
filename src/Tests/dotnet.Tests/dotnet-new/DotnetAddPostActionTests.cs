// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetAddPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetAddPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        private static string TestCsprojFile
        {
            get
            {
                return @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp1.1</TargetFramework>
  </PropertyGroup>
</Project>";
            }
        }

        [Fact(DisplayName = nameof(AddRefFindsOneDefaultProjFileInOutputDirectory))]
        public void AddRefFindsOneDefaultProjFileInOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.proj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.Equal(1, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefFindsOneNameConfiguredProjFileInOutputDirectory))]
        public void AddRefFindsOneNameConfiguredProjFileInOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".fooproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.Equal(1, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefFindsOneNameConfiguredProjFileWhenMultipleExtensionsAreAllowed))]
        public void AddRefFindsOneNameConfiguredProjFileWhenMultipleExtensionsAreAllowed()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".fooproj", ".barproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.Equal(1, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefIgnoresOtherProjectTypesWhenMultipleTypesAreAllowed))]
        public void AddRefIgnoresOtherProjectTypesWhenMultipleTypesAreAllowed()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            string barprojFileFullPath = Path.Combine(targetBasePath, "MyApp.barproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(barprojFileFullPath, TestCsprojFile);

            string csprojFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(csprojFileFullPath, TestCsprojFile);

            string fsprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fsproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fsprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".bazproj", ".fsproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.Equal(1, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefFindsOneDefaultProjFileInAncestorOfOutputDirectory))]
        public void AddRefFindsOneDefaultProjFileInAncestorOfOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.xproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPath, TestCsprojFile);

            string outputBasePath = Path.Combine(targetBasePath, "ChildDir", "GrandchildDir");
            _engineEnvironmentSettings.Host.FileSystem.CreateDirectory(outputBasePath);

            DotnetAddPostActionProcessor actionProcessor = new();
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.Equal(1, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefFindsMultipleDefaultProjFilesInOutputDirectory))]
        public void AddRefFindsMultipleDefaultProjFilesInOutputDirectory()
        {
            string projFilesOriginalContent = TestCsprojFile;
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPathOne = Path.Combine(targetBasePath, "MyApp.anysproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathOne, projFilesOriginalContent);

            string projFileFullPathTwo = Path.Combine(targetBasePath, "MyApp2.someproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathTwo, projFilesOriginalContent);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.Equal(2, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefFindsMultipleDefaultProjFilesInAncestorOfOutputDirectory))]
        public void AddRefFindsMultipleDefaultProjFilesInAncestorOfOutputDirectory()
        {
            string projFilesOriginalContent = TestCsprojFile;
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPathOne = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathOne, projFilesOriginalContent);

            string projFileFullPathTwo = Path.Combine(targetBasePath, "MyApp2.barproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathTwo, projFilesOriginalContent);

            string outputBasePath = Path.Combine(targetBasePath, "ChildDir", "GrandchildDir");

            DotnetAddPostActionProcessor actionProcessor = new();
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.Equal(2, projFilesFound.Count);
        }

        [Fact(DisplayName = nameof(AddRefCanHandleProjectFileRenames))]
        public void AddRefCanHandleProjectFileRenames()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            string referencedProjFileFullPath = Path.Combine(targetBasePath, "NewName.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "project" }, { "reference", "./OldName.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./OldName.csproj", "./NewName.csproj", ChangeKind.Create))
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
            Assert.Equal(referencedProjFileFullPath, callback.Reference);
        }

        [Fact(DisplayName = nameof(AddRefCanHandleProjectFilesWithoutRenames))]
        public void AddRefCanHandleProjectFilesWithoutRenames()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            string referencedProjFileFullPath = Path.Combine(targetBasePath, "Reference.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "project" }, { "reference", "./Reference.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
            Assert.Equal(referencedProjFileFullPath, callback.Reference);
        }

        [Fact(DisplayName = nameof(AddRefCanTargetASingleProjectWithAJsonArray))]
        public void AddRefCanTargetASingleProjectWithAJsonArray()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "package" }, { "reference", "System.Net.Json" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
            Assert.Equal("System.Net.Json", callback.Reference);
        }

        [Fact(DisplayName = nameof(AddRefCanTargetASingleProjectWithTheProjectName))]
        public void AddRefCanTargetASingleProjectWithTheProjectName()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "MyApp.csproj" }, { "referenceType", "package" }, { "reference", "System.Net.Json" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));


            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(projFileFullPath, callback.Target);
            Assert.Equal("System.Net.Json", callback.Reference);
        }

        private class MockAddProjectReferenceCallback
        {
            public string? Target { get; private set; }

            public string? Reference { get; private set; }

            public bool AddProjectReference(string target, string reference)
            {
                if (Target != null)
                {
                    throw new Exception($"{nameof(AddProjectReference)} is called more than once.");
                }

                Target = target;
                Reference = reference;

                return true;
            }

            public bool AddPackageReference(string target, string reference, string? version)
            {
                if (Target != null)
                {
                    throw new Exception($"{nameof(AddPackageReference)} is called more than once.");
                }

                Target = target;
                Reference = reference;

                return true;
            }
        }
    }
}
