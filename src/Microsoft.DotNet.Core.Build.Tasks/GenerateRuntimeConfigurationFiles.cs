// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.ProjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Tasks
{
    /// <summary>
    /// Generates the $(project).runtimeconfig.json and optionally $(project).runtimeconfig.dev.json files
    /// for a project.
    /// </summary>
    public class GenerateRuntimeConfigurationFiles : Task
    {
        [Required]
        public string RuntimeOutputPath { get; set; }

        [Required]
        public string OutputName { get; set; }

        [Required]
        public string LockFilePath { get; set; }

        public string RawRuntimeOptions { get; set; }

        public bool IncludeDevConfig { get; set; }

        private LockFile LockFile { get; set; }

        public override bool Execute()
        {
            LockFile = LockFileCache.Instance.GetLockFile(LockFilePath);

            WriteRuntimeConfig();

            if (IncludeDevConfig)
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
            AddRuntimeOptions(config.RuntimeOptions);

            var runtimeConfigJsonFile =
                Path.Combine(RuntimeOutputPath, OutputName + FileNameSuffixes.RuntimeConfigJson);

            WriteToJsonFile(runtimeConfigJsonFile, config);
        }

        private void AddFramework(RuntimeOptions runtimeOptions)
        {
            // TODO: get this from the lock file once https://github.com/NuGet/Home/issues/2695 is fixed.
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

        private void AddRuntimeOptions(RuntimeOptions runtimeOptions)
        {
            if (string.IsNullOrEmpty(RawRuntimeOptions))
            {
                return;
            }

            var runtimeOptionsFromProject = JObject.Parse(RawRuntimeOptions);
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

            var runtimeConfigDevJsonFile =
                    Path.Combine(RuntimeOutputPath, OutputName + FileNameSuffixes.RuntimeConfigDevJson);

            WriteToJsonFile(runtimeConfigDevJsonFile, devConfig);
        }

        private void AddAdditionalProbingPaths(RuntimeOptions runtimeOptions)
        {
            foreach (var packageFolder in LockFile.PackageFolders)
            {
                if (runtimeOptions.AdditionalProbingPaths == null)
                {
                    runtimeOptions.AdditionalProbingPaths = new List<string>();
                }

                runtimeOptions.AdditionalProbingPaths.Add(packageFolder.Path);
            }
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
