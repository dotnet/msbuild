// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel
{
    internal class ProjectReader
    {
        public static Project GetProject(string projectFile)
        {
            return GetProject(projectFile, new List<DiagnosticMessage>());
        }

        public static Project GetProject(string projectFile, ICollection<DiagnosticMessage> diagnostics)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(projectFile));
            using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return new ProjectReader().ReadProject(stream, name, projectFile, diagnostics);
            }
        }

        public Project ReadProject(Stream stream, string projectName, string projectPath, ICollection<DiagnosticMessage> diagnostics)
        {
            var project = new Project();

            JObject rawProject;
            using (var reader = new StreamReader(stream))
            {
                rawProject = JObject.Parse(reader.ReadToEnd());
            }
            if (rawProject == null)
            {
                throw FileFormatException.Create(
                    "The JSON file can't be deserialized to a JSON object.",
                    projectPath);
            }

            // Meta-data properties
            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            var version = rawProject["version"];
            if (version == null)
            {
                project.Version = new NuGetVersion("1.0.0");
            }
            else
            {
                try
                {
                    var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");
                    project.Version = SpecifySnapshot(version.Value<string>(), buildVersion);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            var fileVersion = Environment.GetEnvironmentVariable("DNX_ASSEMBLY_FILE_VERSION");
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
            project.WebRoot = rawProject.Value<string>("webroot");
            project.EntryPoint = rawProject.Value<string>("entryPoint");
            project.ProjectUrl = rawProject.Value<string>("projectUrl");
            project.LicenseUrl = rawProject.Value<string>("licenseUrl");
            project.IconUrl = rawProject.Value<string>("iconUrl");

            project.Authors = rawProject.ValueAsStringArray("authors");
            project.Owners = rawProject.ValueAsStringArray("owners");
            project.Tags = rawProject.ValueAsStringArray("tags");

            project.Language = rawProject.Value<string>("language");
            project.ReleaseNotes = rawProject.Value<string>("releaseNotes");

            project.RequireLicenseAcceptance = rawProject.Value<bool?>("requireLicenseAcceptance") ?? false;
            project.IsLoadable = rawProject.Value<bool?>("loadable") ?? true;

            project.Dependencies = new List<LibraryRange>();

            // Project files
            project.Files = new ProjectFilesCollection(rawProject, project.ProjectDirectory, project.ProjectFilePath);

            var commands = rawProject.Value<JObject>("commands");
            if (commands != null)
            {
                foreach (var prop in commands.Properties())
                {
                    var value = prop.Value.Value<string>();
                    if (value != null)
                    {
                        project.Commands[prop.Name] = value;
                    }
                }
            }

            var scripts = rawProject.Value<JObject>("scripts");
            if (scripts != null)
            {
                foreach (var prop in scripts.Properties())
                {
                    var stringValue = prop.Value<string>();
                    if (stringValue != null)
                    {
                        project.Scripts[prop.Name] = new string[] { stringValue };
                        continue;
                    }

                    var arrayValue = scripts.ValueAsStringArray(prop.Name);
                    if (arrayValue != null)
                    {
                        project.Scripts[prop.Name] = arrayValue;
                        continue;
                    }

                    throw FileFormatException.Create(
                        string.Format("The value of a script in {0} can only be a string or an array of strings", Project.FileName),
                        prop.Value,
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

            return NuGetVersion.Parse(version);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<LibraryRange> results,
            JObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings.Value<JObject>(propertyName);
            if (dependencies != null)
            {
                foreach (var dependencyKey in dependencies.Properties())
                {
                    if (string.IsNullOrEmpty(dependencyKey.Name))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependencyKey.Value,
                            projectPath);
                    }

                    var dependencyValue = dependencyKey.Value;
                    string dependencyVersionAsString = null;
                    LibraryDependencyType dependencyTypeValue = LibraryDependencyType.Default;
                    LibraryType target = isGacOrFrameworkReference ? LibraryType.ReferenceAssembly : LibraryType.Unspecified;

                    if (dependencyValue.Type == JTokenType.Object)
                    {
                        // "dependencies" : { "Name" : { "version": "1.0", "type": "build", "target": "project" } }
                        var dependencyValueAsObject = (JObject)dependencyValue;
                        dependencyVersionAsString = dependencyValueAsObject.Value<string>("version");

                        // Remove support for flags (we only support build and nothing right now)
                        var type = dependencyValueAsObject.Value<string>("type");
                        if (type != null)
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(type);
                        }

                        // Read the target if specified
                        if (!isGacOrFrameworkReference)
                        {
                            LibraryType parsedTarget;
                            var targetStr = dependencyValueAsObject.Value<string>("target");
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
                            string.Format("Invalid dependency version: {0}. The format is not recognizable.", dependencyKey),
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
                            throw FileFormatException.Create(
                                ex,
                                dependencyValue,
                                projectPath);
                        }
                    }

                    results.Add(new LibraryRange(
                        dependencyKey.Name,
                        dependencyVersionRange,
                        target,
                        dependencyTypeValue,
                        projectPath,
                        ((IJsonLineInfo)dependencyKey.Value).LineNumber,
                        ((IJsonLineInfo)dependencyKey.Value).LinePosition));
                }
            }
        }

        private static bool TryGetStringEnumerable(JObject parent, string property, out IEnumerable<string> result)
        {
            var collection = new List<string>();
            var value = parent[property];
            if (value.Type == JTokenType.String)
            {
                collection.Add(value.Value<string>());
            }
            else if (value.Type == JTokenType.Array)
            {
                collection.AddRange(value.Value<string[]>());
            }
            else
            {
                result = null;
                return false;
            }

            result = collection.SelectMany(v => v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        private void BuildTargetFrameworksAndConfigurations(Project project, JObject projectJsonObject, ICollection<DiagnosticMessage> diagnostics)
        {
            // Get the shared compilationOptions
            project._defaultCompilerOptions = GetCompilationOptions(projectJsonObject) ?? new CompilerOptions();

            project._defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
            {
                Dependencies = new List<LibraryRange>().AsReadOnly()
            };

            // Add default configurations
            project._compilerOptionsByConfiguration["Debug"] = new CompilerOptions
            {
                Defines = new[] { "DEBUG", "TRACE" },
                Optimize = false
            };

            project._compilerOptionsByConfiguration["Release"] = new CompilerOptions
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

            var configurationsSection = projectJsonObject.Value<JObject>("configurations");
            if (configurationsSection != null)
            {
                foreach (var configKey in configurationsSection.Properties())
                {
                    var compilerOptions = GetCompilationOptions(configKey.Value<JObject>());

                    // Only use this as a configuration if it's not a target framework
                    project._compilerOptionsByConfiguration[configKey.Name] = compilerOptions;
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

            var frameworks = projectJsonObject.Value<JObject>("frameworks");
            if (frameworks != null)
            {
                foreach (var frameworkKey in frameworks.Properties())
                {
                    try
                    {
                        var frameworkToken = frameworks.Value<JObject>(frameworkKey.Name);
                        var success = BuildTargetFrameworkNode(project, frameworkKey.Name, frameworkToken);
                        if (!success)
                        {
                            diagnostics?.Add(
                                new DiagnosticMessage(
                                    ErrorCodes.NU1008,
                                    $"\"{frameworkKey}\" is an unsupported framework.",
                                    project.ProjectFilePath,
                                    DiagnosticMessageSeverity.Error,
                                    frameworkToken));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, frameworkKey.Value, project.ProjectFilePath);
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
            var compilerOptions = GetCompilationOptions(frameworkValue) ??
                                  new CompilerOptions();

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

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryRange>()
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

            var binNode = frameworkValue.Value<JObject>("bin");
            if (binNode != null)
            {
                targetFrameworkInformation.AssemblyPath = binNode.Value<string>("assembly");
                targetFrameworkInformation.PdbPath = binNode.Value<string>("pdb");
            }

            project._compilerOptionsByFramework[frameworkName] = compilerOptions;
            project._targetFrameworks[frameworkName] = targetFrameworkInformation;

            return true;
        }

        private static CompilerOptions GetCompilationOptions(JObject rawObject)
        {
            var rawOptions = rawObject.Value<JObject>("compilationOptions");
            if (rawOptions == null)
            {
                return null;
            }

            return new CompilerOptions
            {
                Defines = rawOptions.Value<string[]>("define"),
                LanguageVersion = rawOptions.Value<string>("languageVersion"),
                AllowUnsafe = rawOptions.Value<bool?>("allowUnsafe"),
                Platform = rawOptions.Value<string>("platform"),
                WarningsAsErrors = rawOptions.Value<bool?>("warningsAsErrors"),
                Optimize = rawOptions.Value<bool?>("optimize"),
                KeyFile = rawOptions.Value<string>("keyFile"),
                DelaySign = rawOptions.Value<bool?>("delaySign"),
                StrongName = rawOptions.Value<bool?>("strongName"),
                EmitEntryPoint = rawOptions.Value<bool?>("emitEntryPoint")
            };
        }

        public static string MakeDefaultTargetFrameworkDefine(NuGetFramework targetFramework)
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
    }
}
