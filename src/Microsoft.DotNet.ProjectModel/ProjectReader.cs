// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectReader
    {
        public static bool TryGetProject(string path, out Project project, ICollection<DiagnosticMessage> diagnostics = null, ProjectReaderSettings settings = null)
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
                    project = reader.ReadProject(stream, projectName, projectPath, diagnostics, settings);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public static Project GetProject(string projectPath, ProjectReaderSettings settings = null) => GetProject(projectPath, new List<DiagnosticMessage>(), settings);

        public static Project GetProject(string projectPath, ICollection<DiagnosticMessage> diagnostics, ProjectReaderSettings settings = null)
        {
            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }

            var name = Path.GetFileName(Path.GetDirectoryName(projectPath));

            using (var stream = new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return new ProjectReader().ReadProject(stream, name, projectPath, diagnostics, settings);
            }
        }

        public Project ReadProject(Stream stream, string projectName, string projectPath, ICollection<DiagnosticMessage> diagnostics, ProjectReaderSettings settings = null)
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
            project.Summary = rawProject.Value<string>("summary");
            project.Copyright = rawProject.Value<string>("copyright");
            project.Title = rawProject.Value<string>("title");
            project.EntryPoint = rawProject.Value<string>("entryPoint");
            project.ProjectUrl = rawProject.Value<string>("projectUrl");
            project.LicenseUrl = rawProject.Value<string>("licenseUrl");
            project.IconUrl = rawProject.Value<string>("iconUrl");
            project.CompilerName = rawProject.Value<string>("compilerName") ?? "csc";
            project.TestRunner = rawProject.Value<string>("testRunner");

            project.Authors =
                rawProject.Value<JToken>("authors")?.Values<string>().ToArray() ?? EmptyArray<string>.Value;
            project.Owners = rawProject.Value<JToken>("owners")?.Values<string>().ToArray() ?? EmptyArray<string>.Value;
            project.Tags = rawProject.Value<JToken>("tags")?.Values<string>().ToArray() ?? EmptyArray<string>.Value;

            project.Language = rawProject.Value<string>("language");
            project.ReleaseNotes = rawProject.Value<string>("releaseNotes");

            project.RequireLicenseAcceptance = rawProject.Value<bool>("requireLicenseAcceptance");

            // REVIEW: Move this to the dependencies node?
            project.EmbedInteropTypes = rawProject.Value<bool>("embedInteropTypes");

            project.Dependencies = new List<LibraryRange>();
            project.Tools = new List<LibraryRange>();

            // Project files
            project.Files = new ProjectFilesCollection(rawProject, project.ProjectDirectory, project.ProjectFilePath);

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

            BuildTargetFrameworksAndConfigurations(project, rawProject, diagnostics);

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
            IList<LibraryRange> results,
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
                    string dependencyVersionAsString = null;
                    LibraryType target = isGacOrFrameworkReference ? LibraryType.ReferenceAssembly : LibraryType.Unspecified;

                    if (dependencyValue.Type == JTokenType.Object)
                    {
                        // "dependencies" : { "Name" : { "version": "1.0", "type": "build", "target": "project" } }
                        dependencyVersionAsString = dependencyValue.Value<string>("version");

                        var type = dependencyValue.Value<string>("type");
                        if (type != null)
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(type);
                        }

                        // Read the target if specified
                        if (!isGacOrFrameworkReference)
                        {
                            LibraryType parsedTarget;
                            var targetStr = dependencyValue.Value<string>("target");
                            if (!string.IsNullOrEmpty(targetStr) && LibraryType.TryParse(targetStr, out parsedTarget))
                            {
                                target = parsedTarget;
                            }
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

                    var lineInfo = (IJsonLineInfo)dependencyValue;
                    results.Add(new LibraryRange(
                        dependency.Key,
                        dependencyVersionRange,
                        target,
                        dependencyTypeValue,
                        projectPath,
                        lineInfo.LineNumber,
                        lineInfo.LinePosition));
                }
            }
        }

        private void BuildTargetFrameworksAndConfigurations(Project project, JObject projectJsonObject, ICollection<DiagnosticMessage> diagnostics)
        {
            // Get the shared compilationOptions
            project._defaultCompilerOptions =
                GetCompilationOptions(projectJsonObject, project) ?? new CommonCompilerOptions();

            project._defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
            {
                Dependencies = new List<LibraryRange>()
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
                            diagnostics?.Add(
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
            var frameworkDefine = MakeDefaultTargetFrameworkDefine(frameworkName);

            if (!string.IsNullOrEmpty(frameworkDefine))
            {
                defines.Add(frameworkDefine);
            }

            compilerOptions.Defines = defines;

            var lineInfo = (IJsonLineInfo)frameworkValue;
            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryRange>(),
                CompilerOptions = compilerOptions,
                Line = lineInfo.LineNumber,
                Column = lineInfo.LinePosition
            };

            var frameworkDependencies = new List<LibraryRange>();

            PopulateDependencies(
                project.ProjectFilePath,
                frameworkDependencies,
                frameworkValue,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryRange>();
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

        private static CommonCompilerOptions GetCompilationOptions(JObject rawObject, Project project)
        {
            var rawOptions = rawObject.Value<JToken>("compilationOptions") as JObject;
            if (rawOptions == null)
            {
                return null;
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
                            analyzerOptions.LanguageId = analyzerOption.Value.ToString();
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
                OutputName = rawOptions.Value<string>("outputName")
            };
        }

        private static string MakeDefaultTargetFrameworkDefine(NuGetFramework targetFramework)
        {
            var shortName = targetFramework.GetTwoDigitShortFolderName();

            if (targetFramework.IsPCL)
            {
                return null;
            }

            var candidateName = shortName.ToUpperInvariant();

            // Replace '-', '.', and '+' in the candidate name with '_' because TFMs with profiles use those (like "net40-client")
            // and we want them representable as defines (i.e. "NET40_CLIENT")
            candidateName = candidateName.Replace('-', '_').Replace('+', '_').Replace('.', '_');

            // We require the following from our Target Framework Define names
            // Starts with A-Z or _
            // Contains only A-Z, 0-9 and _
            if (!string.IsNullOrEmpty(candidateName) &&
                (char.IsLetter(candidateName[0]) || candidateName[0] == '_') &&
                candidateName.All(c => Char.IsLetterOrDigit(c) || c == '_'))
            {
                return candidateName;
            }

            return null;
        }

        private static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, Project.FileName);

            return File.Exists(projectPath);
        }
    }
}
