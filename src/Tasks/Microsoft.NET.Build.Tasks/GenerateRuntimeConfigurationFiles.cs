// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.Frameworks;
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

        public bool GenerateRuntimeConfigDevFile { get; set; }

        public bool AlwaysIncludeCoreFramework { get; set; }

        List<ITaskItem> _filesWritten = new();

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
            if (!WriteAdditionalProbingPathsToMainConfig)
            {
                // If we want to generate the runtimeconfig.dev.json file
                // and we have additional probing paths to add to it
                // BUT the runtimeconfigdevpath is empty, log a warning.
                if (GenerateRuntimeConfigDevFile && AdditionalProbingPaths?.Any() == true && string.IsNullOrEmpty(RuntimeConfigDevPath))
                {
                    Log.LogWarning(Strings.SkippingAdditionalProbingPaths);
                }
            }

            if (!string.IsNullOrEmpty(RollForward))
            {
                if (!RollForwardValues.Contains(RollForward, StringComparer.OrdinalIgnoreCase))
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
                    TargetFramework,
                    RuntimeIdentifier,
                    PlatformLibraryName,
                    RuntimeFrameworks,
                    IsSelfContained);

                WriteRuntimeConfig(projectContext.RuntimeFrameworks,
                    projectContext.PlatformLibrary,
                    projectContext.IsFrameworkDependent,
                    projectContext.LockFile.PackageFolders);

                if (GenerateRuntimeConfigDevFile && !string.IsNullOrEmpty(RuntimeConfigDevPath))
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
            RuntimeConfig config = new()
            {
                RuntimeOptions = new RuntimeOptions()
            };

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
            runtimeOptions.Tfm = NuGetFramework.Parse(TargetFrameworkMoniker).GetShortFolderName();

            var frameworks = new List<RuntimeConfigFramework>();
            if (runtimeFrameworks == null || runtimeFrameworks.Length == 0)
            {
                // If the project is not targetting .NET Core, it will not have any platform library (and is marked as non-FrameworkDependent).
                if (lockFilePlatformLibrary != null)
                {
                    //  If there are no RuntimeFrameworks (which would be set in the ProcessFrameworkReferences task based
                    //  on FrameworkReference items), then use package resolved from MicrosoftNETPlatformLibrary for
                    //  the runtimeconfig
                    RuntimeConfigFramework framework = new()
                    {
                        Name = lockFilePlatformLibrary.Name,
                        Version = lockFilePlatformLibrary.Version.ToNormalizedString()
                    };

                    frameworks.Add(framework);
                }
            }
            else
            {
                HashSet<string> usedFrameworkNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (var platformLibrary in runtimeFrameworks)
                {
                    //  In earlier versions of the SDK, we would exclude Microsoft.NETCore.App from the frameworks listed in the runtimeconfig file.
                    //  This was originally a workaround for a bug: https://github.com/dotnet/core-setup/issues/4947
                    //  We would only do this for framework-dependent apps, as the full list was required for self-contained apps.
                    //  As the bug is fixed, we now always include the Microsoft.NETCore.App framework by default for .NET Core 6 and higher
                    if (!AlwaysIncludeCoreFramework &&
                        runtimeFrameworks.Length > 1 &&
                        platformLibrary.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
                        isFrameworkDependent)
                    {
                        continue;
                    }

                    //  Don't add multiple entries for the same shared framework.
                    //  This is necessary if there are FrameworkReferences to different profiles
                    //  that map to the same shared framework.
                    if (!usedFrameworkNames.Add(platformLibrary.Name))
                    {
                        continue;
                    }

                    RuntimeConfigFramework framework = new()
                    {
                        Name = platformLibrary.Name,
                        Version = platformLibrary.Version
                    };

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

            JObject runtimeOptionsFromProject;
            using (JsonTextReader reader = new(File.OpenText(UserRuntimeConfig)))
            {
                runtimeOptionsFromProject = JObject.Load(reader);
            }

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
            RuntimeConfig devConfig = new()
            {
                RuntimeOptions = new RuntimeOptions()
            };

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
            JsonSerializer serializer = new()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            using (JsonTextWriter writer = new(new StreamWriter(File.Create(fileName))))
            {
                serializer.Serialize(writer, value);
            }
        }
    }
}
