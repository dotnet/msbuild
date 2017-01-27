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
        [Required]
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        [Required]
        public string RuntimeConfigPath { get; set; }

        public string RuntimeConfigDevPath { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        /// <summary>
        /// The version of the shared framework that should be used.  If not specified, it will
        /// be the package version of the <see cref="PlatformLibraryName"/> package.
        /// </summary>
        public string RuntimeFrameworkVersion { get; set; }

        public string UserRuntimeConfig { get; set; }

        public ITaskItem[] HostConfigurationOptions { get; set; }

        List<ITaskItem> _filesWritten = new List<ITaskItem>();

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        protected override void ExecuteCore()
        {
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(AssetsFilePath);
            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName);

            WriteRuntimeConfig(projectContext);

            if (!string.IsNullOrEmpty(RuntimeConfigDevPath))
            {
                WriteDevRuntimeConfig(projectContext);
            }
        }

        private void WriteRuntimeConfig(ProjectContext projectContext)
        {
            RuntimeConfig config = new RuntimeConfig();
            config.RuntimeOptions = new RuntimeOptions();

            AddFramework(config.RuntimeOptions, projectContext);
            AddUserRuntimeOptions(config.RuntimeOptions);

            // HostConfigurationOptions are added after AddUserRuntimeOptions so if there are
            // conflicts the HostConfigurationOptions win. The reasoning is that HostConfigurationOptions
            // can be changed using MSBuild properties, which can be specified at build time.
            AddHostConfigurationOptions(config.RuntimeOptions);

            WriteToJsonFile(RuntimeConfigPath, config);
            _filesWritten.Add(new TaskItem(RuntimeConfigPath));
        }

        private void AddFramework(RuntimeOptions runtimeOptions, ProjectContext projectContext)
        {
            if (projectContext.IsPortable)
            {
                var platformLibrary = projectContext.PlatformLibrary;
                if (platformLibrary != null)
                {
                    RuntimeConfigFramework framework = new RuntimeConfigFramework();
                    framework.Name = platformLibrary.Name;
                    if (!string.IsNullOrEmpty(RuntimeFrameworkVersion))
                    {
                        framework.Version = RuntimeFrameworkVersion;
                    }
                    else
                    {
                        framework.Version = platformLibrary.Version.ToNormalizedString();
                    }

                    runtimeOptions.Framework = framework;
                }
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

        private void WriteDevRuntimeConfig(ProjectContext projectContext)
        {
            RuntimeConfig devConfig = new RuntimeConfig();
            devConfig.RuntimeOptions = new RuntimeOptions();

            AddAdditionalProbingPaths(devConfig.RuntimeOptions, projectContext);

            WriteToJsonFile(RuntimeConfigDevPath, devConfig);
            _filesWritten.Add(new TaskItem(RuntimeConfigDevPath));
        }

        private void AddAdditionalProbingPaths(RuntimeOptions runtimeOptions, ProjectContext projectContext)
        {
            foreach (var packageFolder in projectContext.LockFile.PackageFolders)
            {
                if (runtimeOptions.AdditionalProbingPaths == null)
                {
                    runtimeOptions.AdditionalProbingPaths = new List<string>();
                }

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
