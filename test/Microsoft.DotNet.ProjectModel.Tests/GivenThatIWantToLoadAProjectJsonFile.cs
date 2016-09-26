// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using FluentAssertions;
using NuGet.Versioning;
using System.Linq;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToLoadAProjectJsonFile
    {
        private const string ProjectName = "some project name";
        private const string SomeLanguageVersion = "some language version";
        private const string SomeOutputName = "some output name";
        private const string SomeCompilerName = "some compiler name";
        private const string SomePlatform = "some platform";
        private const string SomeKeyFile = "some key file";
        private const string SomeDebugType = "some debug type";
        private const string DependencyName = "some dependency";
        private const string ToolName = "some tool";
        private const string Version = "1.0.0";
        private readonly string ProjectFilePath = AppContext.BaseDirectory;

        private Project _emptyProject;
        private readonly string[] _someDefines = new[] {"DEFINE1", "DEFINE2"};
        private readonly string[] _noWarnings = new[] {"warn1", "warn2"};
        private readonly string[] _someAdditionalArguments = new[] {"additional argument 1", "additional argument 2"};
        private readonly VersionRange _versionRange = VersionRange.Parse(Version);
        private readonly JObject _jsonCompilationOptions;
        private CommonCompilerOptions _commonCompilerOptions;

        public GivenThatIWantToLoadAProjectJsonFile()
        {
            var json = new JObject();
            _emptyProject = GetProject(json);
            _jsonCompilationOptions = new JObject();

            _jsonCompilationOptions.Add("define", new JArray(_someDefines));
            _jsonCompilationOptions.Add("nowarn", new JArray(_noWarnings));
            _jsonCompilationOptions.Add("additionalArguments", new JArray(_someAdditionalArguments));
            _jsonCompilationOptions.Add("languageVersion", SomeLanguageVersion);
            _jsonCompilationOptions.Add("outputName", SomeOutputName);
            _jsonCompilationOptions.Add("compilerName", SomeCompilerName);
            _jsonCompilationOptions.Add("platform", SomePlatform);
            _jsonCompilationOptions.Add("keyFile", SomeKeyFile);
            _jsonCompilationOptions.Add("debugType", SomeDebugType);
            _jsonCompilationOptions.Add("allowUnsafe", true);
            _jsonCompilationOptions.Add("warningsAsErrors", true);
            _jsonCompilationOptions.Add("optimize", true);
            _jsonCompilationOptions.Add("delaySign", true);
            _jsonCompilationOptions.Add("publicSign", true);
            _jsonCompilationOptions.Add("emitEntryPoint", true);
            _jsonCompilationOptions.Add("xmlDoc", true);
            _jsonCompilationOptions.Add("preserveCompilationContext", true);

            _commonCompilerOptions = new CommonCompilerOptions
            {
                Defines = _someDefines,
                SuppressWarnings = _noWarnings,
                AdditionalArguments = _someAdditionalArguments,
                LanguageVersion = SomeLanguageVersion,
                OutputName = SomeOutputName,
                CompilerName = SomeCompilerName,
                Platform = SomePlatform,
                KeyFile = SomeKeyFile,
                DebugType = SomeDebugType,
                AllowUnsafe = true,
                WarningsAsErrors = true,
                Optimize = true,
                DelaySign = true,
                PublicSign = true,
                EmitEntryPoint = true,
                GenerateXmlDocumentation = true,
                PreserveCompilationContext = true
            };
        }

        [Fact]
        public void It_does_not_throw_when_the_project_json_is_empty()
        {
            var json = new JObject();
            Action action = () => GetProject(json);

            action.ShouldNotThrow<Exception>();
        }

        [Fact]
        public void It_sets_Name_to_the_passed_ProjectName_if_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.Name.Should().Be(ProjectName);
        }

        [Fact]
        public void It_sets_Name_to_the_Name_in_the_ProjectJson_when_one_is_set()
        {
            const string nameInProjectJson = "some name in the project.json";
            var json = new JObject();
            json.Add("name", nameInProjectJson);
            var project = GetProject(json);

            project.Name.Should().Be(nameInProjectJson);
        }

        [Fact]
        public void It_sets_the_project_file_path()
        {
            _emptyProject.ProjectFilePath.Should().Be(ProjectFilePath);
        }

        [Fact]
        public void It_sets_the_version_to_one_when_it_is_not_set()
        {
            _emptyProject.Version.Should().Be(new NuGetVersion("1.0.0"));
        }

        [Fact]
        public void It_sets_the_version_to_the_one_in_the_ProjectJson_when_one_is_set()
        {
            var json = new JObject();
            json.Add("version", "1.1");
            var project = GetProject(json);

            project.Version.Should().Be(new NuGetVersion("1.1"));
        }

        [Fact]
        public void It_sets_AssemblyFileVersion_to_the_ProjectJson_version_when_AssemblyFileVersion_is_not_passed_in_the_settings()
        {
            var json = new JObject();
            json.Add("version", "1.1");
            var project = GetProject(json);

            project.AssemblyFileVersion.Should().Be(new NuGetVersion("1.1").Version);
        }

        [Fact]
        public void It_sets_AssemblyFileVersion_Revision_to_the_AssemblyFileVersion_passed_in_the_settings_and_everything_else_to_the_projectJson_Version()
        {
            const int revision = 1;
            var json = new JObject();
            json.Add("version", "1.1");
            var project = GetProject(json, new ProjectReaderSettings { AssemblyFileVersion = revision.ToString() });

            var version = new NuGetVersion("1.1").Version;
            project.AssemblyFileVersion.Should().Be(
                new Version(version.Major, version.Minor, version.Build, revision));
        }

        [Fact]
        public void It_throws_a_FormatException_when_AssemblyFileVersion_passed_in_the_settings_is_invalid()
        {
            var json = new JObject();
            json.Add("version", "1.1");
            Action action = () =>
                GetProject(json, new ProjectReaderSettings { AssemblyFileVersion = "not a revision" });

            action.ShouldThrow<FormatException>().WithMessage("The assembly file version is invalid: not a revision");
        }

        [Fact]
        public void It_leaves_marketing_information_empty_when_it_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.Description.Should().BeNull();
            _emptyProject.PackOptions.Summary.Should().BeNull();
            _emptyProject.Copyright.Should().BeNull();
            _emptyProject.Title.Should().BeNull();
            _emptyProject.EntryPoint.Should().BeNull();
            _emptyProject.PackOptions.ProjectUrl.Should().BeNull();
            _emptyProject.PackOptions.LicenseUrl.Should().BeNull();
            _emptyProject.PackOptions.IconUrl.Should().BeNull();
            _emptyProject.Authors.Should().BeEmpty();
            _emptyProject.PackOptions.Owners.Should().BeEmpty();
            _emptyProject.PackOptions.Tags.Should().BeEmpty();
            _emptyProject.Language.Should().BeNull();
            _emptyProject.PackOptions.ReleaseNotes.Should().BeNull();
        }

        [Fact]
        public void It_sets_the_marketing_information_when_it_is_set_in_the_ProjectJson()
        {
            const string someDescription = "some description";
            const string someSummary = "some summary";
            const string someCopyright = "some copyright";
            const string someTitle = "some title";
            const string someEntryPoint = "some entry point";
            const string someProjectUrl = "some project url";
            const string someLicenseUrl = "some license url";
            const string someIconUrl = "some icon url";
            const string someLanguage = "some language";
            const string someReleaseNotes = "someReleaseNotes";
            var authors = new [] {"some author", "and another author"};
            var owners = new[] {"some owner", "a second owner"};
            var tags = new[] {"tag1", "tag2"};

            var json = new JObject();
            json.Add("description", someDescription);
            json.Add("summary", someSummary);
            json.Add("copyright", someCopyright);
            json.Add("title", someTitle);
            json.Add("entryPoint", someEntryPoint);
            json.Add("projectUrl", someProjectUrl);
            json.Add("licenseUrl", someLicenseUrl);
            json.Add("iconUrl", someIconUrl);
            json.Add("authors", new JArray(authors));
            json.Add("owners", new JArray(owners));
            json.Add("tags", new JArray(tags));
            json.Add("language", someLanguage);
            json.Add("releaseNotes", someReleaseNotes);
            var project = GetProject(json);

            project.Description.Should().Be(someDescription);
            project.PackOptions.Summary.Should().Be(someSummary);
            project.Copyright.Should().Be(someCopyright);
            project.Title.Should().Be(someTitle);
            project.EntryPoint.Should().Be(someEntryPoint);
            project.PackOptions.ProjectUrl.Should().Be(someProjectUrl);
            project.PackOptions.LicenseUrl.Should().Be(someLicenseUrl);
            project.PackOptions.IconUrl.Should().Be(someIconUrl);
            project.Authors.Should().Contain(authors);
            project.PackOptions.Owners.Should().Contain(owners);
            project.PackOptions.Tags.Should().Contain(tags);
            project.Language.Should().Be(someLanguage);
            project.PackOptions.ReleaseNotes.Should().Be(someReleaseNotes);
        }

        [Fact]
        public void It_sets_the_marketing_information_when_it_is_set_in_the_ProjectJson_PackOptions()
        {
            const string someDescription = "some description";
            const string someSummary = "some summary";
            const string someCopyright = "some copyright";
            const string someTitle = "some title";
            const string someEntryPoint = "some entry point";
            const string someProjectUrl = "some project url";
            const string someLicenseUrl = "some license url";
            const string someIconUrl = "some icon url";
            const string someLanguage = "some language";
            const string someReleaseNotes = "someReleaseNotes";
            var authors = new[] { "some author", "and another author" };
            var owners = new[] { "some owner", "a second owner" };
            var tags = new[] { "tag1", "tag2" };

            var json = new JObject();
            var packOptions = new JObject();

            json.Add("description", someDescription);
            json.Add("copyright", someCopyright);
            json.Add("title", someTitle);
            json.Add("entryPoint", someEntryPoint);
            json.Add("authors", new JArray(authors));
            json.Add("language", someLanguage);
            packOptions.Add("summary", someSummary);
            packOptions.Add("projectUrl", someProjectUrl);
            packOptions.Add("licenseUrl", someLicenseUrl);
            packOptions.Add("iconUrl", someIconUrl);
            packOptions.Add("owners", new JArray(owners));
            packOptions.Add("tags", new JArray(tags));
            packOptions.Add("releaseNotes", someReleaseNotes);
            json.Add("packOptions", packOptions);

            var project = GetProject(json);

            project.Description.Should().Be(someDescription);
            project.PackOptions.Summary.Should().Be(someSummary);
            project.Copyright.Should().Be(someCopyright);
            project.Title.Should().Be(someTitle);
            project.EntryPoint.Should().Be(someEntryPoint);
            project.PackOptions.ProjectUrl.Should().Be(someProjectUrl);
            project.PackOptions.LicenseUrl.Should().Be(someLicenseUrl);
            project.PackOptions.IconUrl.Should().Be(someIconUrl);
            project.Authors.Should().Contain(authors);
            project.PackOptions.Owners.Should().Contain(owners);
            project.PackOptions.Tags.Should().Contain(tags);
            project.Language.Should().Be(someLanguage);
            project.PackOptions.ReleaseNotes.Should().Be(someReleaseNotes);
        }

        [Fact]
        public void It_warns_when_deprecated_schema_is_used()
        {
            var json = new JObject();

            json.Add("compilerName", "some compiler");
            json.Add("compilationOptions", new JObject());
            json.Add("projectUrl", "some project url");
            json.Add("compile", "something");
            json.Add("resource", "something");
            json.Add("content", "something");
            json.Add("packInclude", "something");
            json.Add("publishExclude", "something");

            var project = GetProject(json);

            project.Diagnostics.Should().HaveCount(8);

            project.Diagnostics.Should().Contain(m =>
                m.ErrorCode == ErrorCodes.DOTNET1015 &&
                m.Severity == DiagnosticMessageSeverity.Warning &&
                m.Message == "The 'compilationOptions' option is deprecated. Use 'buildOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
                m.ErrorCode == ErrorCodes.DOTNET1016 &&
                m.Severity == DiagnosticMessageSeverity.Warning &&
                m.Message == "The 'projectUrl' option in the root is deprecated. Use it in 'packOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
                m.ErrorCode == ErrorCodes.DOTNET1016 &&
                m.Severity == DiagnosticMessageSeverity.Warning &&
                m.Message == "The 'compilerName' option in the root is deprecated. Use it in 'buildOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
               m.ErrorCode == ErrorCodes.DOTNET1015 &&
               m.Severity == DiagnosticMessageSeverity.Warning &&
               m.Message == "The 'compile' option is deprecated. Use 'compile' in 'buildOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
               m.ErrorCode == ErrorCodes.DOTNET1015 &&
               m.Severity == DiagnosticMessageSeverity.Warning &&
               m.Message == "The 'resource' option is deprecated. Use 'embed' in 'buildOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
               m.ErrorCode == ErrorCodes.DOTNET1015 &&
               m.Severity == DiagnosticMessageSeverity.Warning &&
               m.Message == "The 'content' option is deprecated. Use 'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output instead.");

            project.Diagnostics.Should().Contain(m =>
               m.ErrorCode == ErrorCodes.DOTNET1015 &&
               m.Severity == DiagnosticMessageSeverity.Warning &&
               m.Message == "The 'packInclude' option is deprecated. Use 'files' in 'packOptions' instead.");

            project.Diagnostics.Should().Contain(m =>
               m.ErrorCode == ErrorCodes.DOTNET1015 &&
               m.Severity == DiagnosticMessageSeverity.Warning &&
               m.Message == "The 'publishExclude' option is deprecated. Use 'publishOptions' instead.");
        }

        [Fact]
        public void It_sets_the_compilerName_to_csc_when_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.GetCompilerOptions(targetFramework: null, configurationName: null).CompilerName.Should().Be("csc");
        }

        [Fact]
        public void It_sets_the_compilerName_to_the_one_in_the_ProjectJson()
        {
            const string compilerName = "a compiler different from csc";
            var json = new JObject();
            json.Add("compilerName", compilerName);
            var project = GetProject(json);

            project.GetCompilerOptions(targetFramework: null, configurationName: null).CompilerName.Should().Be(compilerName);
        }

        [Fact]
        public void It_leaves_testRunner_null_when_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.TestRunner.Should().BeNull();
        }

        [Fact]
        public void It_sets_testRunner_to_the_one_in_the_ProjectJson()
        {
            const string someTestRunner = "some test runner";
            var json = new JObject();
            json.Add("testRunner", someTestRunner);
            var project = GetProject(json);

            project.TestRunner.Should().Be(someTestRunner);
        }

        [Fact]
        public void It_sets_requireLicenseAcceptance_to_false_when_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.PackOptions.RequireLicenseAcceptance.Should().BeFalse();
        }

        [Fact]
        public void It_sets_requireLicenseAcceptance_to_true_when_it_is_true_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("requireLicenseAcceptance", true);
            var project = GetProject(json);

            project.PackOptions.RequireLicenseAcceptance.Should().BeTrue();
        }

        [Fact]
        public void It_sets_requireLicenseAcceptance_to_false_when_it_is_false_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("requireLicenseAcceptance", false);
            var project = GetProject(json);

            project.PackOptions.RequireLicenseAcceptance.Should().BeFalse();
        }

        [Fact]
        public void It_sets_embedInteropTypes_to_false_when_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.EmbedInteropTypes.Should().BeFalse();
        }

        [Fact]
        public void It_sets_embedInteropTypes_to_true_when_it_is_true_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("embedInteropTypes", true);
            var project = GetProject(json);

            project.EmbedInteropTypes.Should().BeTrue();
        }

        [Fact]
        public void It_sets_embedInteropTypes_to_false_when_it_is_false_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("embedInteropTypes", false);
            var project = GetProject(json);

            project.EmbedInteropTypes.Should().BeFalse();
        }

        [Fact]
        public void It_does_not_add_commands_when_commands_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.Commands.Should().BeEmpty();
        }

        [Fact]
        public void It_does_not_add_commands_when_commands_is_not_a_JsonObject()
        {
            var json = new JObject();
            json.Add("commands", true);
            var project = GetProject(json);

            project.Commands.Should().BeEmpty();
        }

        [Fact]
        public void It_does_not_add_the_commands_when_its_value_is_not_a_string()
        {
            var json = new JObject();
            var commands = new JObject();
            json.Add("commands", commands);

            commands.Add("commandKey1", "commandValue1");
            commands.Add("commandKey2", true);

            var project = GetProject(json);

            project.Commands.Count.Should().Be(1);
            project.Commands.First().Key.Should().Be("commandKey1");
            project.Commands.First().Value.Should().Be("commandValue1");
        }

        [Fact]
        public void It_does_not_add_scripts_when_scripts_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.Scripts.Should().BeEmpty();
        }

        [Fact]
        public void It_does_not_add_scripts_when_scripts_is_not_a_JsonObject()
        {
            var json = new JObject();
            json.Add("scripts", true);
            var project = GetProject(json);

            project.Scripts.Should().BeEmpty();
        }

        [Fact]
        public void It_adds_the_scripts_when_its_value_is_either_a_string_or_an_array_of_strings()
        {
            var scriptArrayValues = new [] {"scriptValue2", "scriptValue3"};

            var json = new JObject();
            var scripts = new JObject();
            json.Add("scripts", scripts);

            scripts.Add("scriptKey1", "scriptValue1");
            scripts.Add("scriptKey3", new JArray(scriptArrayValues));

            var project = GetProject(json);

            project.Scripts.Count.Should().Be(2);
            project.Scripts.First().Key.Should().Be("scriptKey1");
            project.Scripts.First().Value.Should().Contain("scriptValue1");
            project.Scripts["scriptKey3"].Should().Contain(scriptArrayValues);
        }

        [Fact]
        public void It_throws_when_the_value_of_a_script_is_neither_a_string_nor_array_of_strings()
        {
            var json = new JObject();
            var scripts = new JObject();
            json.Add("scripts", scripts);

            scripts.Add("scriptKey2", true);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("The value of a script in project.json can only be a string or an array of strings");
        }

        [Fact]
        public void It_uses_an_empty_compiler_options_when_one_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.GetCompilerOptions(null, null).Should().Be(new CommonCompilerOptions
            {
                OutputName = ProjectName,
                CompilerName = "csc"
            });
        }

        [Fact]
        public void It_sets_analyzerOptions_when_it_is_set_in_the_compilationOptions_in_the_ProjectJson()
        {
            var json = new JObject();
            var compilationOptions = new JObject();
            json.Add("compilationOptions", compilationOptions);

            var analyzerOptions = new JObject();
            compilationOptions.Add("analyzerOptions", analyzerOptions);

            analyzerOptions.Add("languageId", "C#");

            var project = GetProject(json);
            project.AnalyzerOptions.LanguageId.Should().Be("C#");
        }

        [Fact]
        public void It_throws_when_the_analyzerOptions_languageId_is_not_a_string()
        {
            var json = new JObject();
            var compilationOptions = new JObject();
            json.Add("compilationOptions", compilationOptions);

            var analyzerOptions = new JObject();
            compilationOptions.Add("analyzerOptions", analyzerOptions);

            analyzerOptions.Add("languageId", true);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>().WithMessage("The analyzer languageId must be a string");
        }

        [Fact]
        public void It_throws_when_the_analyzerOptions_has_no_languageId()
        {
            var json = new JObject();
            var compilationOptions = new JObject();
            json.Add("compilationOptions", compilationOptions);

            var analyzerOptions = new JObject();
            compilationOptions.Add("analyzerOptions", analyzerOptions);

            analyzerOptions.Add("differentFromLanguageId", "C#");

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("Unrecognized analyzerOption key: differentFromLanguageId");
        }

        [Fact]
        public void It_sets_compilationOptions_when_it_is_set_in_the_compilationOptions_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("compilationOptions", _jsonCompilationOptions);

            var project = GetProject(json);

            project.GetCompilerOptions(null, null).Should().Be(_commonCompilerOptions);
        }

        [Fact]
        public void It_sets_buildOptions_when_it_is_set_in_the_compilationOptions_in_the_ProjectJson()
        {
            var json = new JObject();
            json.Add("buildOptions", _jsonCompilationOptions);

            var project = GetProject(json);

            project.GetCompilerOptions(null, null).Should().Be(_commonCompilerOptions);
        }

        [Fact]
        public void It_merges_configuration_sections_set_in_the_ProjectJson()
        {
            var json = new JObject();
            var configurations = new JObject();
            json.Add("compilationOptions", _jsonCompilationOptions);
            json.Add("configurations", configurations);

            _jsonCompilationOptions["allowUnsafe"] = null;

            var someConfiguration = new JObject();
            configurations.Add("some configuration", someConfiguration);
            var someConfigurationCompilationOptions = new JObject();
            someConfiguration.Add("compilationOptions", someConfigurationCompilationOptions);
            someConfigurationCompilationOptions.Add("allowUnsafe", false);

            var project = GetProject(json);

            _commonCompilerOptions.AllowUnsafe = false;

            project.GetCompilerOptions(null, "some configuration").Should().Be(_commonCompilerOptions);
        }

        [Fact]
        public void It_does_not_set_rawRuntimeOptions_when_it_is_not_set_in_the_ProjectJson()
        {
            _emptyProject.RawRuntimeOptions.Should().BeNull();
        }

        [Fact]
        public void It_throws_when_runtimeOptions_is_not_a_Json_object()
        {
            var json = new JObject();
            json.Add("runtimeOptions", "not a json object");

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>().WithMessage("The runtimeOptions must be an object");
        }

        [Fact]
        public void It_sets_the_rawRuntimeOptions_serialized_when_it_is_set_in_the_ProjectJson()
        {
            var configProperties = new JObject();
            configProperties.Add("System.GC.Server", true);
            var runtimeOptions = new JObject();
            runtimeOptions.Add("configProperties", configProperties);
            var json = new JObject();
            json.Add("runtimeOptions", runtimeOptions);

            var project = GetProject(json);

            project.RawRuntimeOptions.Should().Be(runtimeOptions.ToString());
        }

        [Fact]
        public void Dependencies_is_empty_when_no_dependencies_and_no_tools_are_set_in_the_ProjectJson()
        {
            _emptyProject.Dependencies.Should().BeEmpty();
        }

        [Fact]
        public void It_throws_when_the_dependency_has_no_name_set()
        {
            var dependencies = new JObject();
            dependencies.Add("", "1.0.0");
            var json = new JObject();
            json.Add("dependencies", dependencies);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>().WithMessage("Unable to resolve dependency ''.");
        }

        [Fact]
        public void It_throws_when_the_dependency_value_is_not_an_object_not_a_string()
        {
            var dependencies = new JObject();
            dependencies.Add(DependencyName, true);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage($"Invalid dependency version: {DependencyName}. The format is not recognizable.");
        }

        [Fact]
        public void It_throws_when_the_dependency_version_is_not_valid_when_set_directly()
        {
            var dependencies = new JObject();
            dependencies.Add(DependencyName, "some invalid version");
            var json = new JObject();
            json.Add("dependencies", dependencies);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("'some invalid version' is not a valid version string.");
        }

        [Fact]
        public void It_throws_when_the_dependency_version_is_not_valid_when_set_in_an_object()
        {
            var dependency = new JObject();
            dependency.Add("version", "some invalid version");
            var dependencies = new JObject();
            dependencies.Add(DependencyName, dependency);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("'some invalid version' is not a valid version string.");
        }

        [Fact]
        public void It_leaves_version_null_when_it_is_set_to_empty_string()
        {
            var dependencies = new JObject();
            dependencies.Add(DependencyName, string.Empty);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();

            dependency.LibraryRange.VersionRange.Should().BeNull();
        }

        [Fact]
        public void It_adds_the_dependency_when_the_version_is_set_directly()
        {
            var dependencies = new JObject();
            dependencies.Add(DependencyName, Version);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();
            dependency.Name.Should().Be(DependencyName);
            dependency.LibraryRange.VersionRange.Should().Be(_versionRange);
            dependency.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.All);
            dependency.Type.Should().Be(LibraryDependencyType.Default);
            dependency.SourceFilePath.Should().Be(ProjectFilePath);
            dependency.SourceLine.Should().Be(3);
            dependency.SourceColumn.Should().Be(30);
        }

        [Fact]
        public void It_adds_the_dependency_when_the_version_is_set_in_an_object()
        {
            var dependencyJson = new JObject();
            dependencyJson.Add("version", Version);
            var dependencies = new JObject();
            dependencies.Add(DependencyName, dependencyJson);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();
            dependency.Name.Should().Be(DependencyName);
            dependency.LibraryRange.VersionRange.Should().Be(_versionRange);
            dependency.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.All);
            dependency.Type.Should().Be(LibraryDependencyType.Default);
            dependency.SourceFilePath.Should().Be(ProjectFilePath);
            dependency.SourceLine.Should().Be(3);
            dependency.SourceColumn.Should().Be(24);
        }

        [Fact]
        public void It_sets_the_dependency_type_when_it_is_set_in_the_dependency_in_the_ProjectJson()
        {
            var dependencyJson = new JObject();
            dependencyJson.Add("type", "build");
            var dependencies = new JObject();
            dependencies.Add(DependencyName, dependencyJson);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();
            dependency.Type.Should().Be(LibraryDependencyType.Build);
        }

        [Fact]
        public void It_leaves_the_dependency_target_Unspecified_when_it_fails_to_parse_the_set_target_in_the_ProjectJson()
        {
            var dependencyJson = new JObject();
            dependencyJson.Add("target", "not a valid target");
            var dependencies = new JObject();
            dependencies.Add(DependencyName, dependencyJson);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();
            dependency.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.None);
        }

        [Fact]
        public void It_sets_the_dependency_target_when_it_is_set_in_the_ProjectJson()
        {
            var dependencyJson = new JObject();
            dependencyJson.Add("target", "Project");
            var dependencies = new JObject();
            dependencies.Add(DependencyName, dependencyJson);
            var json = new JObject();
            json.Add("dependencies", dependencies);

            var project = GetProject(json);

            var dependency = project.Dependencies.First();
            dependency.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.Project);
        }

        [Fact]
        public void It_throws_when_the_tool_has_no_name_set()
        {
            var tools = new JObject();
            tools.Add("", "1.0.0");
            var json = new JObject();
            json.Add("tools", tools);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>().WithMessage("Unable to resolve dependency ''.");
        }

        [Fact]
        public void It_throws_when_the_tool_value_is_not_an_object_not_a_string()
        {
            var tools = new JObject();
            tools.Add(ToolName, true);
            var json = new JObject();
            json.Add("tools", tools);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage($"Invalid dependency version: {ToolName}. The format is not recognizable.");
        }

        [Fact]
        public void It_throws_when_the_tool_version_is_not_valid_when_set_directly()
        {
            var tools = new JObject();
            tools.Add(ToolName, "some invalid version");
            var json = new JObject();
            json.Add("tools", tools);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("'some invalid version' is not a valid version string.");
        }

        [Fact]
        public void It_throws_when_the_tool_version_is_not_valid_when_set_in_an_object()
        {
            var tool = new JObject();
            tool.Add("version", "some invalid version");
            var tools = new JObject();
            tools.Add(ToolName, tool);
            var json = new JObject();
            json.Add("tools", tools);

            Action action = () => GetProject(json);

            action.ShouldThrow<FileFormatException>()
                .WithMessage("'some invalid version' is not a valid version string.");
        }

        [Fact]
        public void It_leaves_the_tools_version_null_when_it_is_set_to_empty_string()
        {
            var tools = new JObject();
            tools.Add(ToolName, string.Empty);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();

            tool.LibraryRange.VersionRange.Should().BeNull();
        }

        [Fact]
        public void It_adds_the_tool_when_the_version_is_set_directly()
        {
            var tools = new JObject();
            tools.Add(ToolName, Version);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();
            tool.Name.Should().Be(ToolName);
            tool.LibraryRange.VersionRange.Should().Be(_versionRange);
            tool.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.All);
            tool.Type.Should().Be(LibraryDependencyType.Default);
            tool.SourceFilePath.Should().Be(ProjectFilePath);
            tool.SourceLine.Should().Be(3);
            tool.SourceColumn.Should().Be(24);
        }

        [Fact]
        public void It_adds_the_tool_when_the_version_is_set_in_an_object()
        {
            var toolJson = new JObject();
            toolJson.Add("version", Version);
            var tools = new JObject();
            tools.Add(ToolName, toolJson);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();
            tool.Name.Should().Be(ToolName);
            tool.LibraryRange.VersionRange.Should().Be(_versionRange);
            tool.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.All);
            tool.Type.Should().Be(LibraryDependencyType.Default);
            tool.SourceFilePath.Should().Be(ProjectFilePath);
            tool.SourceLine.Should().Be(3);
            tool.SourceColumn.Should().Be(18);
        }

        [Fact]
        public void It_sets_the_tool_type_when_it_is_set_in_the_tool_in_the_ProjectJson()
        {
            var toolJson = new JObject();
            toolJson.Add("type", "build");
            var tools = new JObject();
            tools.Add(ToolName, toolJson);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();
            tool.Type.Should().Be(LibraryDependencyType.Build);
        }

        [Fact]
        public void It_leaves_the_tool_target_Unspecified_when_it_fails_to_parse_the_set_target_in_the_ProjectJson()
        {
            var toolJson = new JObject();
            toolJson.Add("target", "not a valid target");
            var tools = new JObject();
            tools.Add(ToolName, toolJson);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();
            tool.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.None);
        }

        [Fact]
        public void It_sets_the_tool_target_when_it_is_set_in_the_ProjectJson()
        {
            var toolJson = new JObject();
            toolJson.Add("target", "Project");
            var tools = new JObject();
            tools.Add(ToolName, toolJson);
            var json = new JObject();
            json.Add("tools", tools);

            var project = GetProject(json);

            var tool = project.Tools.First();
            tool.LibraryRange.TypeConstraint.Should().Be(LibraryDependencyTarget.Project);
        }

        public Project GetProject(JObject json, ProjectReaderSettings settings = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var sw = new StreamWriter(stream, Encoding.UTF8, 256, true))
                {
                    using (var writer = new JsonTextWriter(sw))
                    {
                        writer.Formatting = Formatting.Indented;
                        json.WriteTo(writer);
                    }

                    stream.Position = 0;
                    var projectReader = new ProjectReader();
                    return projectReader.ReadProject(
                        stream,
                        ProjectName,
                        ProjectFilePath,
                        settings);
                }
            }
        }
    }
}
