// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel.Files;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    internal class ProjectReader : IProjectReader
    {
        public static bool TryGetProject(string path, out Project project, ProjectReaderSettings settings = null)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), Project.FileName, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasProjectFile(path))
            {
                return false;
            }
            else
            {
                projectPath = Path.Combine(path, Project.FileName);
            }

            // Assume the directory name is the project name if none was specified
            var projectName = PathUtility.GetDirectoryName(Path.GetFullPath(path));
            projectPath = Path.GetFullPath(projectPath);

            if (!File.Exists(projectPath))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    var reader = new ProjectReader();
                    project = reader.ReadProject(stream, projectName, projectPath, settings);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public static Project GetProject(string projectPath, ProjectReaderSettings settings = null)
        {
            return new ProjectReader().ReadProject(projectPath, settings);
        }

        public Project ReadProject(string projectPath, ProjectReaderSettings settings)
        {
            projectPath = ProjectPathHelper.NormalizeProjectFilePath(projectPath);

            var name = Path.GetFileName(Path.GetDirectoryName(projectPath));

            using (var stream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadProject(stream, name, projectPath, settings);
            }
        }

        public Project ReadProject(Stream stream, string projectName, string projectPath, ProjectReaderSettings settings = null)
        {
            settings = settings ?? new ProjectReaderSettings();
            var project = new Project();

            var reader = new StreamReader(stream);
            JObject rawProject;
            using (var jsonReader = new JsonTextReader(reader))
            {
                rawProject = JObject.Load(jsonReader);

                // Try to read another token to ensure we're at the end of the document.
                // This will no-op if we are, and throw a JsonReaderException if there is additional content (which is what we want)
                jsonReader.Read();
            }

            if (rawProject == null)
            {
                throw FileFormatException.Create(
                    "The JSON file can't be deserialized to a JSON object.",
                    projectPath);
            }

            // Meta-data properties
            project.Name = rawProject.Value<string>("name") ?? projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            var version = rawProject.Value<string>("version");
            if (version == null)
            {
                project.Version = new NuGetVersion("1.0.0");
            }
            else
            {
                try
                {
                    var buildVersion = settings.VersionSuffix;
                    project.Version = SpecifySnapshot(version, buildVersion);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            var fileVersion = settings.AssemblyFileVersion;
            if (string.IsNullOrWhiteSpace(fileVersion))
            {
                project.AssemblyFileVersion = project.Version.Version;
            }
            else
            {
                try
                {
                    var simpleVersion = project.Version.Version;
                    project.AssemblyFileVersion = new Version(simpleVersion.Major,
                        simpleVersion.Minor,
                        simpleVersion.Build,
                        int.Parse(fileVersion));
                }
                catch (FormatException ex)
                {
                    throw new FormatException("The assembly file version is invalid: " + fileVersion, ex);
                }
            }

            project.Description = rawProject.Value<string>("description");
            project.Copyright = rawProject.Value<string>("copyright");
            project.Title = rawProject.Value<string>("title");
            project.EntryPoint = rawProject.Value<string>("entryPoint");
            project.TestRunner = rawProject.Value<string>("testRunner");
            project.Authors =
                rawProject.Value<JToken>("authors")?.Values<string>().ToArray() ?? EmptyArray<string>.Value;
            project.Language = rawProject.Value<string>("language");

            // REVIEW: Move this to the dependencies node?
            project.EmbedInteropTypes = rawProject.Value<bool>("embedInteropTypes");

            project.Dependencies = new List<ProjectLibraryDependency>();

            project.Tools = new List<ProjectLibraryDependency>();

            project.Runtimes = new List<string>(); 

            // Project files
            project.Files = new ProjectFilesCollection(rawProject, project.ProjectDirectory, project.ProjectFilePath);
            AddProjectFilesCollectionDiagnostics(rawProject, project);

            var commands = rawProject.Value<JToken>("commands") as JObject;
            if (commands != null)
            {
                foreach (var command in commands)
                {
                    var commandValue = command.Value.Type == JTokenType.String ? command.Value.Value<string>() : null;
                    if (commandValue != null)
                    {
                        project.Commands[command.Key] = commandValue;
                    }
                }
            }

            var scripts = rawProject.Value<JToken>("scripts") as JObject;
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var stringValue = script.Value.Type == JTokenType.String ? script.Value.Value<string>() : null;
                    if (stringValue != null)
                    {
                        project.Scripts[script.Key] = new string[] { stringValue };
                        continue;
                    }

                    var arrayValue =
                        script.Value.Type == JTokenType.Array ? script.Value.Values<string>().ToArray() : null;
                    if (arrayValue != null)
                    {
                        project.Scripts[script.Key] = arrayValue;
                        continue;
                    }

                    throw FileFormatException.Create(
                        string.Format("The value of a script in {0} can only be a string or an array of strings", Project.FileName),
                        script.Value,
                        project.ProjectFilePath);
                }
            }

            project.PackOptions = GetPackOptions(rawProject, project) ?? new PackOptions();
            project.RuntimeOptions = GetRuntimeOptions(rawProject) ?? new RuntimeOptions();
            project.PublishOptions = GetPublishInclude(rawProject, project);

            BuildTargetFrameworksAndConfigurations(project, rawProject);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Tools,
                rawProject,
                "tools",
                isGacOrFrameworkReference: false);
            
            PopulateRuntimes(project.Runtimes, rawProject); 
 

            JToken runtimeOptionsToken;
            if (rawProject.TryGetValue("runtimeOptions", out runtimeOptionsToken))
            {
                var runtimeOptions = runtimeOptionsToken as JObject;
                if (runtimeOptions == null)
                {
                    throw FileFormatException.Create("The runtimeOptions must be an object", runtimeOptionsToken);
                }

                project.RawRuntimeOptions = runtimeOptions.ToString();
            }

            return project;
        }

        private static NuGetVersion SpecifySnapshot(string version, string snapshotValue)
        {
            if (version.EndsWith("-*"))
            {
                if (string.IsNullOrEmpty(snapshotValue))
                {
                    version = version.Substring(0, version.Length - 2);
                }
                else
                {
                    version = version.Substring(0, version.Length - 1) + snapshotValue;
                }
            }

            return new NuGetVersion(version);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<ProjectLibraryDependency> results,
            JObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings.Value<JToken>(propertyName) as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    if (string.IsNullOrEmpty(dependency.Key))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependency.Key,
                            projectPath);
                    }

                    var dependencyValue = dependency.Value;
                    var dependencyTypeValue = LibraryDependencyType.Default;
                    
                    var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                    var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                    var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;

                    var target = isGacOrFrameworkReference ? LibraryDependencyTarget.Reference : LibraryDependencyTarget.All;
                    string dependencyVersionAsString = null;

                    if (dependencyValue.Type == JTokenType.Object)
                    {
                        // "dependencies" : { "Name" : { "version": "1.0", "type": "build", "target": "project" } }
                        dependencyVersionAsString = dependencyValue.Value<string>("version");

                        var type = dependencyValue.Value<string>("type");
                        if (type != null)
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(new [] { type });
                        }

                        // Read the target if specified
                        if (!isGacOrFrameworkReference)
                        {
                            var targetStr = dependencyValue.Value<string>("target");
                            target = LibraryDependencyTargetUtils.Parse(targetStr);
                        }
                        
                        IEnumerable<string> strings;
                        if (TryGetStringEnumerable(dependencyValue["include"], out strings))
                        {
                            dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }

                        if (TryGetStringEnumerable(dependencyValue["exclude"], out strings))
                        {
                            dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }

                        if (TryGetStringEnumerable(dependencyValue["suppressParent"], out strings))
                        {
                            // This overrides any settings that came from the type property.
                            suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }
                    }
                    else if (dependencyValue.Type == JTokenType.String)
                    {
                        // "dependencies" : { "Name" : "1.0" }
                        dependencyVersionAsString = dependencyValue.Value<string>();
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format(
                                "Invalid dependency version: {0}. The format is not recognizable.",
                                dependency.Key),
                            dependencyValue,
                            projectPath);
                    }

                    VersionRange dependencyVersionRange = null;
                    if (!string.IsNullOrEmpty(dependencyVersionAsString))
                    {
                        try
                        {
                            dependencyVersionRange = VersionRange.Parse(dependencyVersionAsString);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(ex, dependencyValue, projectPath);
                        }
                    }

                    // the dependency flags are: Include flags - Exclude flags
                    var includeFlags = dependencyIncludeFlagsValue & ~dependencyExcludeFlagsValue;

                    var lineInfo = (IJsonLineInfo)dependencyValue;
                    results.Add(new ProjectLibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            dependency.Key,
                            dependencyVersionRange,
                            target),
                        Type = dependencyTypeValue,
                        IncludeType = includeFlags,
                        SuppressParent = suppressParentFlagsValue,
                        SourceFilePath = projectPath,
                        SourceLine = lineInfo.LineNumber,
                        SourceColumn = lineInfo.LinePosition
                    });
                }
            }
        }
 
        private static void PopulateRuntimes(IList<string> results, JObject settings) 
        { 
            var runtimes = settings.Value<JToken>("runtimes") as JObject; 
            if (runtimes != null) 
            { 
                foreach (var runtime in runtimes) 
                { 
                    if (!string.IsNullOrEmpty(runtime.Key)) 
                    { 
                        results.Add(runtime.Key); 
                    } 
                } 
            } 
        }

        private void BuildTargetFrameworksAndConfigurations(Project project, JObject projectJsonObject)
        {
            // Get the shared compilationOptions
            project._defaultCompilerOptions =
                GetCompilationOptions(projectJsonObject, project) ?? new CommonCompilerOptions { CompilerName = "csc" };

            project._defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
            {
                Dependencies = new List<ProjectLibraryDependency>()
            };

            // Add default configurations
            project._compilerOptionsByConfiguration["Debug"] = new CommonCompilerOptions
            {
                Defines = new[] { "DEBUG", "TRACE" },
                Optimize = false
            };

            project._compilerOptionsByConfiguration["Release"] = new CommonCompilerOptions
            {
                Defines = new[] { "RELEASE", "TRACE" },
                Optimize = true
            };

            // The configuration node has things like debug/release compiler settings
            /*
                {
                    "configurations": {
                        "Debug": {
                        },
                        "Release": {
                        }
                    }
                }
            */

            var configurationsSection = projectJsonObject.Value<JToken>("configurations") as JObject;
            if (configurationsSection != null)
            {
                foreach (var configKey in configurationsSection)
                {
                    var compilerOptions = GetCompilationOptions(configKey.Value as JObject, project);

                    // Only use this as a configuration if it's not a target framework
                    project._compilerOptionsByConfiguration[configKey.Key] = compilerOptions;
                }
            }

            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "dnxcore50": {
                        }
                    }
                }
            */

            var frameworks = projectJsonObject.Value<JToken>("frameworks") as JObject;
            if (frameworks != null)
            {
                foreach (var framework in frameworks)
                {
                    try
                    {
                        var frameworkToken = framework.Value as JObject;
                        var success = BuildTargetFrameworkNode(project, framework.Key, frameworkToken);
                        if (!success)
                        {
                            var lineInfo = (IJsonLineInfo)framework.Value;
                            project.Diagnostics.Add(
                                new DiagnosticMessage(
                                    ErrorCodes.NU1008,
                                    $"\"{framework.Key}\" is an unsupported framework.",
                                    project.ProjectFilePath,
                                    DiagnosticMessageSeverity.Error,
                                    lineInfo.LineNumber,
                                    lineInfo.LinePosition));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, framework.Value, project.ProjectFilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Parse a Json object which represents project configuration for a specified framework
        /// </summary>
        /// <param name="frameworkKey">The name of the framework</param>
        /// <param name="frameworkValue">The Json object represent the settings</param>
        /// <returns>Returns true if it successes.</returns>
        private bool BuildTargetFrameworkNode(Project project, string frameworkKey, JObject frameworkValue)
        {
            // If no compilation options are provided then figure them out from the node
            var compilerOptions = GetCompilationOptions(frameworkValue, project) ??
                                  new CommonCompilerOptions();

            var frameworkName = NuGetFramework.Parse(frameworkKey);

            // If it's not unsupported then keep it
            if (frameworkName.IsUnsupported)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            // Add the target framework specific define
            var defines = new HashSet<string>(compilerOptions.Defines ?? Enumerable.Empty<string>());

            compilerOptions.Defines = defines;

            var lineInfo = (IJsonLineInfo)frameworkValue;
            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<ProjectLibraryDependency>(),
                CompilerOptions = compilerOptions,
                Line = lineInfo.LineNumber,
                Column = lineInfo.LinePosition,
                Imports = GetImports(frameworkValue)
            };

            var frameworkDependencies = new List<ProjectLibraryDependency>();

            PopulateDependencies(
                project.ProjectFilePath,
                frameworkDependencies,
                frameworkValue,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<ProjectLibraryDependency>();
            PopulateDependencies(
                project.ProjectFilePath,
                frameworkAssemblies,
                frameworkValue,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkDependencies.AddRange(frameworkAssemblies);
            targetFrameworkInformation.Dependencies = frameworkDependencies;

            targetFrameworkInformation.WrappedProject = frameworkValue.Value<string>("wrappedProject");

            var binNode = frameworkValue.Value<JToken>("bin") as JObject;
            if (binNode != null)
            {
                targetFrameworkInformation.AssemblyPath = binNode.Value<string>("assembly");
            }

            project._targetFrameworks[frameworkName] = targetFrameworkInformation;

            return true;
        }

        private IEnumerable<string> GetImports(JObject frameworkValue)
        {
            var prop = frameworkValue.Property("imports");
            if (prop == null)
            {
                return Enumerable.Empty<string>();
            }

            if (prop.Value.Type == JTokenType.Array)
            {
                return (prop.Value as JArray).Select(i => i.Value<string>());
            }
            else if (prop.Value.Type == JTokenType.String)
            {
                return new [] { prop.Value.ToString() };
            }

            return null;
        }

        private static CommonCompilerOptions GetCompilationOptions(JObject rawObject, Project project)
        {
            var compilerName = rawObject.Value<string>("compilerName");
            if (compilerName != null)
            {
                var lineInfo = rawObject.Value<IJsonLineInfo>("compilerName");
                project.Diagnostics.Add(
                    new DiagnosticMessage(
                        ErrorCodes.DOTNET1016,
                        $"The 'compilerName' option in the root is deprecated. Use it in 'buildOptions' instead.",
                        project.ProjectFilePath,
                        DiagnosticMessageSeverity.Warning,
                        lineInfo.LineNumber,
                        lineInfo.LinePosition));
            }

            var rawOptions = rawObject.Value<JToken>("buildOptions") as JObject;
            if (rawOptions == null)
            {
                rawOptions = rawObject.Value<JToken>("compilationOptions") as JObject;
                if (rawOptions == null)
                {
                    return new CommonCompilerOptions
                    {
                        CompilerName = compilerName ?? "csc"
                    };
                }

                var lineInfo = (IJsonLineInfo)rawOptions;

                project.Diagnostics.Add(
                    new DiagnosticMessage(
                        ErrorCodes.DOTNET1015,
                        $"The 'compilationOptions' option is deprecated. Use 'buildOptions' instead.",
                        project.ProjectFilePath,
                        DiagnosticMessageSeverity.Warning,
                        lineInfo.LineNumber,
                        lineInfo.LinePosition));
            }

            var analyzerOptionsJson = rawOptions.Value<JToken>("analyzerOptions") as JObject;
            if (analyzerOptionsJson != null)
            {
                var analyzerOptions = new AnalyzerOptions();

                foreach (var analyzerOption in analyzerOptionsJson)
                {
                    switch (analyzerOption.Key)
                    {
                        case "languageId":
                            if (analyzerOption.Value.Type != JTokenType.String)
                            {
                                throw FileFormatException.Create(
                                    "The analyzer languageId must be a string",
                                    analyzerOption.Value.ToString(),
                                    project.ProjectFilePath);
                            }
                            analyzerOptions = new AnalyzerOptions(analyzerOption.Value.ToString());
                            break;

                        default:
                            throw FileFormatException.Create(
                               $"Unrecognized analyzerOption key: {analyzerOption.Key}",
                               project.ProjectFilePath);
                    }
                }

                project.AnalyzerOptions = analyzerOptions;
            }

            return new CommonCompilerOptions
            {
                Defines = rawOptions.Value<JToken>("define")?.Values<string>().ToArray(),
                SuppressWarnings = rawOptions.Value<JToken>("nowarn")?.Values<string>().ToArray(),
                AdditionalArguments = rawOptions.Value<JToken>("additionalArguments")?.Values<string>().ToArray(),
                LanguageVersion = rawOptions.Value<string>("languageVersion"),
                AllowUnsafe = rawOptions.Value<bool?>("allowUnsafe"),
                Platform = rawOptions.Value<string>("platform"),
                WarningsAsErrors = rawOptions.Value<bool?>("warningsAsErrors"),
                Optimize = rawOptions.Value<bool?>("optimize"),
                KeyFile = rawOptions.Value<string>("keyFile"),
                DelaySign = rawOptions.Value<bool?>("delaySign"),
                PublicSign = rawOptions.Value<bool?>("publicSign"),
                DebugType = rawOptions.Value<string>("debugType"),
                EmitEntryPoint = rawOptions.Value<bool?>("emitEntryPoint"),
                GenerateXmlDocumentation = rawOptions.Value<bool?>("xmlDoc"),
                PreserveCompilationContext = rawOptions.Value<bool?>("preserveCompilationContext"),
                OutputName = rawOptions.Value<string>("outputName"),
                CompilerName = rawOptions.Value<string>("compilerName") ?? compilerName ?? "csc",
                CompileInclude = GetIncludeContext(
                    project,
                    rawOptions,
                    "compile",
                    defaultBuiltInInclude: ProjectFilesCollection.SdkInjectedDefaultCompileBuiltInPatterns,
                    defaultBuiltInExclude: ProjectFilesCollection.DefaultBuiltInExcludePatterns),
                EmbedInclude = GetIncludeContext(
                    project,
                    rawOptions,
                    "embed",
                    defaultBuiltInInclude: null,
                    defaultBuiltInExclude: ProjectFilesCollection.DefaultBuiltInExcludePatterns),
                CopyToOutputInclude = GetIncludeContext(
                    project,
                    rawOptions,
                    "copyToOutput",
                    defaultBuiltInInclude: null,
                    defaultBuiltInExclude: null)
            };
        }

        private static IncludeContext GetIncludeContext(
            Project project,
            JObject rawOptions,
            string option,
            string[] defaultBuiltInInclude,
            string[] defaultBuiltInExclude)
        {
            var contextOption = rawOptions.Value<JToken>(option);
            if (contextOption != null)
            {
                return new IncludeContext(
                    project.ProjectDirectory,
                    option,
                    rawOptions,
                    defaultBuiltInInclude,
                    defaultBuiltInExclude);
            }

            return null;
        }

        private static PackOptions GetPackOptions(JObject rawProject, Project project)
        {
            var rawPackOptions = rawProject.Value<JToken>("packOptions") as JObject;

            // Files to be packed along with the project
            IncludeContext packInclude = null;
            if (rawPackOptions != null && rawPackOptions.Value<JToken>("files") != null)
            {
                packInclude = new IncludeContext(
                    project.ProjectDirectory,
                    "files",
                    rawPackOptions,
                    defaultBuiltInInclude: null,
                    defaultBuiltInExclude: ProjectFilesCollection.DefaultBuiltInExcludePatterns);
            }

            var repository = GetPackOptionsValue<JToken>("repository", rawProject, rawPackOptions, project) as JObject;

            return new PackOptions
            {
                ProjectUrl = GetPackOptionsValue<string>("projectUrl", rawProject, rawPackOptions, project),
                LicenseUrl = GetPackOptionsValue<string>("licenseUrl", rawProject, rawPackOptions, project),
                IconUrl = GetPackOptionsValue<string>("iconUrl", rawProject, rawPackOptions, project),
                Owners = GetPackOptionsValue<JToken>("owners", rawProject, rawPackOptions, project)?.Values<string>().ToArray() ?? EmptyArray<string>.Value,
                Tags = GetPackOptionsValue<JToken>("tags", rawProject, rawPackOptions, project)?.Values<string>().ToArray() ?? EmptyArray<string>.Value,
                ReleaseNotes = GetPackOptionsValue<string>("releaseNotes", rawProject, rawPackOptions, project),
                RequireLicenseAcceptance = GetPackOptionsValue<bool>("requireLicenseAcceptance", rawProject, rawPackOptions, project),
                Summary = GetPackOptionsValue<string>("summary", rawProject, rawPackOptions, project),
                RepositoryType = repository?.Value<string>("type"),
                RepositoryUrl = repository?.Value<string>("url"),
                PackInclude = packInclude
            };
        }

        private static T GetPackOptionsValue<T>(
            string option,
            JObject rawProject,
            JObject rawPackOptions,
            Project project)
        {
            var rootValue = rawProject.Value<T>(option);
            if (rawProject.GetValue(option) != null)
            {
                var lineInfo = rawProject.Value<IJsonLineInfo>(option);
                project.Diagnostics.Add(
                    new DiagnosticMessage(
                        ErrorCodes.DOTNET1016,
                        $"The '{option}' option in the root is deprecated. Use it in 'packOptions' instead.",
                        project.ProjectFilePath,
                        DiagnosticMessageSeverity.Warning,
                        lineInfo.LineNumber,
                        lineInfo.LinePosition));
            }

            if (rawPackOptions != null)
            {
                var packOptionValue = rawPackOptions.Value<T>(option);
                if (packOptionValue != null)
                {
                    return packOptionValue;
                }
            }

            return rootValue;
        }

        private static RuntimeOptions GetRuntimeOptions(JObject rawProject)
        {
            var rawRuntimeOptions = rawProject.Value<JToken>("runtimeOptions") as JObject;
            if (rawRuntimeOptions == null)
            {
                return null;
            }

            return new RuntimeOptions
            {
                // Value<T>(null) will return default(T) which is false in this case.
                GcServer = rawRuntimeOptions.Value<bool>("gcServer"),
                GcConcurrent = rawRuntimeOptions.Value<bool>("gcConcurrent")
            };
        }

        private static IncludeContext GetPublishInclude(JObject rawProject, Project project)
        {
            var rawPublishOptions = rawProject.Value<JToken>("publishOptions");
            if (rawPublishOptions != null)
            {
                return new IncludeContext(
                    project.ProjectDirectory,
                    "publishOptions",
                    rawProject,
                    defaultBuiltInInclude: null,
                    defaultBuiltInExclude: null);
            }

            return null;
        }

        private static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, Project.FileName);

            return File.Exists(projectPath);
        }

        private static void AddProjectFilesCollectionDiagnostics(JObject rawProject, Project project)
        {
            ConvertDeprecatedCompileOptions(rawProject, project);
            ConvertDeprecatedContentOptions(rawProject, project);
            ConvertDeprecatedResourceOptions(rawProject, project);

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                "'files' in 'packOptions'",
                "packInclude",
                new string[] { "packOptions" },
                "files");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                "'publishOptions'",
                "publishExclude",
                new string[] { "publishOptions" },
                "excludeFiles");
        }

        private static void ConvertDeprecatedCompileOptions(JObject rawProject, Project project)
        {
            var compileWarning = "'compile' in 'buildOptions'";
            var compileObjectHierarchy = new string[] { "buildOptions", "compile" };

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                compileWarning,
                "compile",
                compileObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                compileWarning,
                "compileExclude",
                compileObjectHierarchy,
                "excludeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                compileWarning,
                "compileFiles",
                compileObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                compileWarning,
                "compileBuiltIn",
                compileObjectHierarchy,
                "builtIns");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                compileWarning,
                "exclude",
                compileObjectHierarchy,
                "exclude");
        }

        private static void ConvertDeprecatedContentOptions(JObject rawProject, Project project)
        {
            var contentWarning = "'publishOptions' to publish or 'copyToOutput' in 'buildOptions' to copy to build output";
            var copyToOutputObjectHierarchy = new string[] { "buildOptions", "copyToOutput" };
            var publishObjectHierarchy = new string[] { "publishOptions" };

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "content",
                copyToOutputObjectHierarchy,
                "includeFiles");
            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "content",
                publishObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentExclude",
                copyToOutputObjectHierarchy,
                "excludeFiles");
            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentExclude",
                publishObjectHierarchy,
                "excludeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentFiles",
                copyToOutputObjectHierarchy,
                "includeFiles");
            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentFiles",
                publishObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentFiles",
                copyToOutputObjectHierarchy,
                "contentBuiltIn");
            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                contentWarning,
                "contentBuiltIn",
                publishObjectHierarchy,
                "builtIns");
        }

        private static void ConvertDeprecatedResourceOptions(JObject rawProject, Project project)
        {
            var resourceWarning = "'embed' in 'buildOptions'";
            var embedObjectHierarchy = new string[] { "buildOptions", "embed" };

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "resource",
                embedObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "resourceExclude",
                embedObjectHierarchy,
                "excludeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "resourceFiles",
                embedObjectHierarchy,
                "includeFiles");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "resourceBuiltIn",
                embedObjectHierarchy,
                "builtIns");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "namedResource",
                embedObjectHierarchy,
                "mappings");

            ConvertFromDeprecatedFormat(
                rawProject,
                project,
                resourceWarning,
                "exclude",
                embedObjectHierarchy,
                "exclude");
        }

        private static void ConvertFromDeprecatedFormat(
            JObject rawProject,
            Project project,
            string message,
            string deprecatedKey,
            string[] newKeyObjectHierarchy,
            string newKey
            )
        {
            var deprecatedValue = rawProject.Value<IJsonLineInfo>(deprecatedKey);
            if (deprecatedValue != null)
            {
                var currentObject = rawProject;
                foreach (var key in newKeyObjectHierarchy)
                {
                    var childObject = currentObject.Value<JToken>(key) as JObject;
                    if (childObject == null)
                    {
                        childObject = new JObject();
                        currentObject[key] = childObject;
                    }

                    currentObject = childObject;
                }

                if (deprecatedValue is JObject)
                {
                    currentObject[newKey] = (deprecatedValue as JObject).DeepClone();
                    //(currentObject[newKey] as JObject).Merge((deprecatedValue as JObject).DeepClone(), new JsonMergeSettings
                    //{
                    //    // union array values together to avoid duplicates
                    //    MergeArrayHandling = MergeArrayHandling.Union
                    //});
                }
                else if (deprecatedValue is JToken)
                {
                    currentObject[newKey] = (deprecatedValue as JToken).DeepClone();
                    //JToken.FromObject(x.Concat(x))
                }

                project.Diagnostics.Add(
                    new DiagnosticMessage(
                        ErrorCodes.DOTNET1015,
                        $"The '{deprecatedKey}' option is deprecated. Use {message} instead.",
                        project.ProjectFilePath,
                        DiagnosticMessageSeverity.Warning,
                        deprecatedValue.LineNumber,
                        deprecatedValue.LinePosition));
            }
        }

        private static bool TryGetStringEnumerable(JToken token, out IEnumerable<string> result)
        {
            IEnumerable<string> values;
            if (token == null)
            {
                result = null;
                return false;
            }
            else if (token.Type == JTokenType.String)
            {
                values = new[]
                    {
                        token.Value<string>()
                    };
            }
            else
            {
                values = token.Values<string>();
            }
            result = values
                .SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }
    }
}
