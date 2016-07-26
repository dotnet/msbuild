// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.ProjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            var json = new JObject();
            var runtimeOptions = new JObject();
            json.Add("runtimeOptions", runtimeOptions);

            WriteFramework(runtimeOptions);
            WriteRuntimeOptions(runtimeOptions);

            var runtimeConfigJsonFile =
                Path.Combine(RuntimeOutputPath, OutputName + FileNameSuffixes.RuntimeConfigJson);

            using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigJsonFile))))
            {
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        }

        private void WriteFramework(JObject runtimeOptions)
        {
            // TODO: get this from the lock file once https://github.com/NuGet/Home/issues/2695 is fixed.
            var packageName = "Microsoft.NETCore.App";

            var redistExport = LockFile.Libraries.FirstOrDefault(e => e.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
            if (redistExport != null)
            {
                var framework = new JObject(
                    new JProperty("name", redistExport.Name),
                    new JProperty("version", redistExport.Version.ToNormalizedString()));
                runtimeOptions.Add("framework", framework);
            }
        }

        private void WriteRuntimeOptions(JObject runtimeOptions)
        {
            if (string.IsNullOrEmpty(RawRuntimeOptions))
            {
                return;
            }

            var runtimeOptionsFromProject = JObject.Parse(RawRuntimeOptions);
            foreach (var runtimeOption in runtimeOptionsFromProject)
            {
                runtimeOptions.Add(runtimeOption.Key, runtimeOption.Value);
            }
        }

        private void WriteDevRuntimeConfig()
        {
            var json = new JObject();
            var runtimeOptions = new JObject();
            json.Add("runtimeOptions", runtimeOptions);

            AddAdditionalProbingPaths(runtimeOptions);

            var runtimeConfigDevJsonFile =
                    Path.Combine(RuntimeOutputPath, OutputName + FileNameSuffixes.RuntimeConfigDevJson);

            using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigDevJsonFile))))
            {
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        }

        private void AddAdditionalProbingPaths(JObject runtimeOptions)
        {
            var additionalProbingPaths = new JArray();
            foreach (var packageFolder in LockFile.PackageFolders)
            {
                additionalProbingPaths.Add(packageFolder.Path);
            }

            runtimeOptions.Add("additionalProbingPaths", additionalProbingPaths);
        }
    }
}
