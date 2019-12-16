// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).runtimeconfig.json and optionally $(project).runtimeconfig.dev.json files
    /// for a project.
    /// </summary>
    public class GenerateRuntimeConfigurationFiles : TaskBase
    {
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        [Required]
        public string RuntimeConfigPath { get; set; }

        public string RuntimeConfigDevPath { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] RuntimeFrameworks { get; set; }

        public string RollForward { get; set; }

        public string UserRuntimeConfig { get; set; }

        public ITaskItem[] HostConfigurationOptions { get; set; }

        public ITaskItem[] AdditionalProbingPaths { get; set; }

        public bool IsSelfContained { get; set; }

        public bool WriteAdditionalProbingPathsToMainConfig { get; set; }

        public bool WriteIncludedFrameworks { get; set; }

        List<ITaskItem> _filesWritten = new List<ITaskItem>();

        private static readonly string[] RollForwardValues = new string[]
        {
            "Disable",
            "LatestPatch",
            "Minor",
            "LatestMinor",
            "Major",
            "LatestMajor"
        };

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        protected override void ExecuteCore()
        {
            bool writeDevRuntimeConfig = !string.IsNullOrEmpty(RuntimeConfigDevPath);

            if (!WriteAdditionalProbingPathsToMainConfig)
            {
                if (AdditionalProbingPaths?.Any() == true && !writeDevRuntimeConfig)
                {
                    Log.LogWarning(Strings.SkippingAdditionalProbingPaths);
                }
            }

            if (!string.IsNullOrEmpty(RollForward))
            {
                if (!RollForwardValues.Any(v => string.Equals(RollForward, v, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.LogError(Strings.InvalidRollForwardValue, RollForward, string.Join(", ", RollForwardValues));
                    return;
                }
            }

            if (AssetsFilePath == null)
            {
                var isFrameworkDependent = LockFileExtensions.IsFrameworkDependent(
                    RuntimeFrameworks,
                    IsSelfContained,
                    RuntimeIdentifier,
                    string.IsNullOrWhiteSpace(PlatformLibraryName));

                if (isFrameworkDependent != true)
                {
                    throw new ArgumentException(
                        $"{nameof(DependencyContextBuilder)} Does not support non FrameworkDependent without asset file. " +
                        $"runtimeFrameworks: {string.Join(",", RuntimeFrameworks.Select(r => r.ItemSpec))} " +
                        $"isSelfContained: {IsSelfContained} " +
                        $"runtimeIdentifier: {RuntimeIdentifier} " +
                        $"platformLibraryName: {PlatformLibraryName}");
                }

                if (PlatformLibraryName != null)
                {
                    throw new ArgumentException(
                        "Does not support non null PlatformLibraryName(TFM < 3) without asset file.");
                }

                WriteRuntimeConfig(
                    RuntimeFrameworks.Select(r => new ProjectContext.RuntimeFramework(r)).ToArray(),
                    null,
                    isFrameworkDependent: true, new List<LockFileItem>());
            }
            else
            {
                LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);

                ProjectContext projectContext = lockFile.CreateProjectContext(
                    NuGetUtils.ParseFrameworkName(TargetFrameworkMoniker),
                    RuntimeIdentifier,
                    PlatformLibraryName,
                    RuntimeFrameworks,
                    IsSelfContained);

                WriteRuntimeConfig(projectContext.RuntimeFrameworks,
                    projectContext.PlatformLibrary,
                    projectContext.IsFrameworkDependent,
                    projectContext.LockFile.PackageFolders);

                if (writeDevRuntimeConfig)
                {
                    WriteDevRuntimeConfig(projectContext.LockFile.PackageFolders);
                }
            }
        }

        private void WriteRuntimeConfig(
            ProjectContext.RuntimeFramework[] runtimeFrameworks,
            LockFileTargetLibrary platformLibrary,
            bool isFrameworkDependent,
            IList<LockFileItem> packageFolders)
        {
            RuntimeConfig config = new RuntimeConfig();
            config.RuntimeOptions = new RuntimeOptions();

            AddFrameworks(
                config.RuntimeOptions,
                runtimeFrameworks,
                platformLibrary,
                isFrameworkDependent);
            AddUserRuntimeOptions(config.RuntimeOptions);

            // HostConfigurationOptions are added after AddUserRuntimeOptions so if there are
            // conflicts the HostConfigurationOptions win. The reasoning is that HostConfigurationOptions
            // can be changed using MSBuild properties, which can be specified at build time.
            AddHostConfigurationOptions(config.RuntimeOptions);

            if (WriteAdditionalProbingPathsToMainConfig)
            {
                AddAdditionalProbingPaths(config.RuntimeOptions, packageFolders);
            }

            WriteToJsonFile(RuntimeConfigPath, config);
            _filesWritten.Add(new TaskItem(RuntimeConfigPath));
        }

        private void AddFrameworks(RuntimeOptions runtimeOptions,
                                   ProjectContext.RuntimeFramework[] runtimeFrameworks,
                                   LockFileTargetLibrary lockFilePlatformLibrary,
                                   bool isFrameworkDependent)
        {
            runtimeOptions.Tfm = TargetFramework;

            var frameworks = new List<RuntimeConfigFramework>();
            if (runtimeFrameworks == null || runtimeFrameworks.Length == 0)
            {
                // If the project is not targetting .NET Core, it will not have any platform library (and is marked as non-FrameworkDependent).
                if (lockFilePlatformLibrary != null)
                {
                    //  If there are no RuntimeFrameworks (which would be set in the ProcessFrameworkReferences task based
                    //  on FrameworkReference items), then use package resolved from MicrosoftNETPlatformLibrary for
                    //  the runtimeconfig
                    RuntimeConfigFramework framework = new RuntimeConfigFramework();
                    framework.Name = lockFilePlatformLibrary.Name;
                    framework.Version = lockFilePlatformLibrary.Version.ToNormalizedString();

                    frameworks.Add(framework);
                }
            }
            else
            {
                HashSet<string> usedFrameworkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var platformLibrary in runtimeFrameworks)
                {
                    if (runtimeFrameworks.Length > 1 &&
                        platformLibrary.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
                        isFrameworkDependent)
                    {
                        //  If there are multiple runtime frameworks, then exclude Microsoft.NETCore.App,
                        //  as a workaround for https://github.com/dotnet/core-setup/issues/4947
                        //  The workaround only applies to normal framework references, included frameworks
                        //  (in self-contained apps) must list all frameworks.
                        continue;
                    }

                    //  Don't add multiple entries for the same shared framework.
                    //  This is necessary if there are FrameworkReferences to different profiles
                    //  that map to the same shared framework.
                    if (!usedFrameworkNames.Add(platformLibrary.Name))
                    {
                        continue;
                    }

                    RuntimeConfigFramework framework = new RuntimeConfigFramework();
                    framework.Name = platformLibrary.Name;
                    framework.Version = platformLibrary.Version;

                    frameworks.Add(framework);
                }
            }

            if (isFrameworkDependent)
            {
                runtimeOptions.RollForward = RollForward;

                //  If there is only one runtime framework, then it goes in the framework property of the json
                //  If there are multiples, then we leave the framework property unset and put the list in
                //  the frameworks property.
                if (frameworks.Count == 1)
                {
                    runtimeOptions.Framework = frameworks[0];
                }
                else
                {
                    runtimeOptions.Frameworks = frameworks;
                }
            }
            else if (WriteIncludedFrameworks)
            {
                //  Self-contained apps don't have framework references, instead write the frameworks
                //  into the includedFrameworks property.
                runtimeOptions.IncludedFrameworks = frameworks;
            }
        }

        private void AddUserRuntimeOptions(RuntimeOptions runtimeOptions)
        {
            if (string.IsNullOrEmpty(UserRuntimeConfig) || !File.Exists(UserRuntimeConfig))
            {
                return;
            }

            var rawRuntimeOptions = File.ReadAllText(UserRuntimeConfig);

            var runtimeOptionsFromProject = JObject.Parse(rawRuntimeOptions);
            foreach (var runtimeOption in runtimeOptionsFromProject)
            {
                runtimeOptions.RawOptions.Add(runtimeOption.Key, runtimeOption.Value);
            }
        }

        private void AddHostConfigurationOptions(RuntimeOptions runtimeOptions)
        {
            if (HostConfigurationOptions == null || !HostConfigurationOptions.Any())
            {
                return;
            }

            JObject configProperties = GetConfigProperties(runtimeOptions);

            foreach (var hostConfigurationOption in HostConfigurationOptions)
            {
                configProperties[hostConfigurationOption.ItemSpec] = GetConfigPropertyValue(hostConfigurationOption);
            }
        }

        private static JObject GetConfigProperties(RuntimeOptions runtimeOptions)
        {
            JToken configProperties;
            if (!runtimeOptions.RawOptions.TryGetValue("configProperties", out configProperties)
                || configProperties == null
                || configProperties.Type != JTokenType.Object)
            {
                configProperties = new JObject();
                runtimeOptions.RawOptions["configProperties"] = configProperties;
            }

            return (JObject)configProperties;
        }

        private static JToken GetConfigPropertyValue(ITaskItem hostConfigurationOption)
        {
            string valueString = hostConfigurationOption.GetMetadata("Value");

            bool boolValue;
            if (bool.TryParse(valueString, out boolValue))
            {
                return new JValue(boolValue);
            }

            int intValue;
            if (int.TryParse(valueString, out intValue))
            {
                return new JValue(intValue);
            }

            return new JValue(valueString);
        }

        private void WriteDevRuntimeConfig(IList<LockFileItem> packageFolders)
        {
            RuntimeConfig devConfig = new RuntimeConfig();
            devConfig.RuntimeOptions = new RuntimeOptions();

            AddAdditionalProbingPaths(devConfig.RuntimeOptions, packageFolders);

            WriteToJsonFile(RuntimeConfigDevPath, devConfig);
            _filesWritten.Add(new TaskItem(RuntimeConfigDevPath));
        }

        private void AddAdditionalProbingPaths(RuntimeOptions runtimeOptions, IList<LockFileItem> packageFolders)
        {
            if (runtimeOptions.AdditionalProbingPaths == null)
            {
                runtimeOptions.AdditionalProbingPaths = new List<string>();
            }

            // Add the specified probing paths first so they are probed first
            if (AdditionalProbingPaths?.Any() == true)
            {
                foreach (var additionalProbingPath in AdditionalProbingPaths)
                {
                    runtimeOptions.AdditionalProbingPaths.Add(additionalProbingPath.ItemSpec);
                }
            }

            foreach (var packageFolder in packageFolders)
            {
                // DotNetHost doesn't handle additional probing paths with a trailing slash
                runtimeOptions.AdditionalProbingPaths.Add(EnsureNoTrailingDirectorySeparator(packageFolder.Path));
            }
        }

        private static string EnsureNoTrailingDirectorySeparator(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                char lastChar = path[path.Length - 1];
                if (lastChar == Path.DirectorySeparatorChar)
                {
                    path = path.Substring(0, path.Length - 1);
                }
            }

            return path;
        }

        private static void WriteToJsonFile(string fileName, object value)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            serializer.Formatting = Formatting.Indented;
            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;

            using (JsonTextWriter writer = new JsonTextWriter(new StreamWriter(File.Create(fileName))))
            {
                serializer.Serialize(writer, value);
            }
        }
    }
}
