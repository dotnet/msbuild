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

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).runtimeconfig.json and optionally $(project).runtimeconfig.dev.json files
    /// for a project.
    /// </summary>
    public class GenerateRuntimeConfigurationFiles : Task
    {
        [Required]
        public string LockFilePath { get; set; }

        [Required]
        public string RuntimeConfigPath { get; set; }

        public string RuntimeConfigDevPath { get; set; }

        public string UserRuntimeConfig { get; set; }

        private LockFile LockFile { get; set; }

        public override bool Execute()
        {
            LockFile = new LockFileCache(BuildEngine4).GetLockFile(LockFilePath);

            WriteRuntimeConfig();

            if (!string.IsNullOrEmpty(RuntimeConfigDevPath))
            {
                WriteDevRuntimeConfig();
            }

            return true;
        }

        private void WriteRuntimeConfig()
        {
            RuntimeConfig config = new RuntimeConfig();
            config.RuntimeOptions = new RuntimeOptions();

            AddFramework(config.RuntimeOptions);
            AddUserRuntimeOptions(config.RuntimeOptions);

            WriteToJsonFile(RuntimeConfigPath, config);
        }

        private void AddFramework(RuntimeOptions runtimeOptions)
        {
            // TODO: https://github.com/dotnet/sdk/issues/17 get this from the lock file
            var packageName = "Microsoft.NETCore.App";

            var redistExport = LockFile
                .Libraries
                .FirstOrDefault(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));

            if (redistExport != null)
            {
                RuntimeConfigFramework framework = new RuntimeConfigFramework();
                framework.Name = redistExport.Name;
                framework.Version = redistExport.Version.ToNormalizedString();

                runtimeOptions.Framework = framework;
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

        private void WriteDevRuntimeConfig()
        {
            RuntimeConfig devConfig = new RuntimeConfig();
            devConfig.RuntimeOptions = new RuntimeOptions();

            AddAdditionalProbingPaths(devConfig.RuntimeOptions);

            WriteToJsonFile(RuntimeConfigDevPath, devConfig);
        }

        private void AddAdditionalProbingPaths(RuntimeOptions runtimeOptions)
        {
            foreach (var packageFolder in LockFile.PackageFolders)
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
