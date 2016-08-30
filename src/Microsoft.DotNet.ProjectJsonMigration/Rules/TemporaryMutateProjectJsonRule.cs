// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    /// <summary>
    /// This rule is temporary while project.json still exists in the new project system.
    /// It renames your existing project.json (if output directory is the current project directory),
    /// creates a copy, then mutates that copy.
    /// 
    /// Mutations:
    ///  - inject a dependency on the Microsoft.SDK targets
    ///  - removing the "runtimes" node.
    /// </summary>
    public class TemporaryMutateProjectJsonRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            bool shouldRenameOldProject = PathsAreEqual(migrationSettings.OutputDirectory, migrationSettings.ProjectDirectory);
            
            if (!shouldRenameOldProject && File.Exists(Path.Combine(migrationSettings.OutputDirectory, "project.json")))
            {
                // TODO: should there be a setting to overwrite anything in output directory?
                throw new Exception("Existing project.json found in output directory.");
            }

            var sourceProjectFile = Path.Combine(migrationSettings.ProjectDirectory, "project.json");
            var destinationProjectFile = Path.Combine(migrationSettings.OutputDirectory, "project.json");
            if (shouldRenameOldProject)
            {
                var renamedProjectFile = Path.Combine(migrationSettings.ProjectDirectory, "project.migrated.json");
                File.Move(sourceProjectFile, renamedProjectFile);
                sourceProjectFile = renamedProjectFile;
            }

            var json = CreateDestinationProjectFile(sourceProjectFile, destinationProjectFile);
            InjectSdkReference(json, ConstantPackageNames.CSdkPackageName, migrationSettings.SdkPackageVersion);
            RemoveRuntimesNode(json);

            File.WriteAllText(destinationProjectFile, json.ToString());
        }

        private JObject CreateDestinationProjectFile(string sourceProjectFile, string destinationProjectFile)
        {
            File.Copy(sourceProjectFile, destinationProjectFile);
            return JObject.Parse(File.ReadAllText(destinationProjectFile));
        }

        private void InjectSdkReference(JObject json, string sdkPackageName, string sdkPackageVersion)
        {
            JToken dependenciesNode;
            if (json.TryGetValue("dependencies", out dependenciesNode))
            {
                var dependenciesNodeObject = dependenciesNode.Value<JObject>();
                dependenciesNodeObject.Add(sdkPackageName, sdkPackageVersion);
            }
            else
            {
                var dependenciesNodeObject = new JObject();
                dependenciesNodeObject.Add(sdkPackageName, sdkPackageVersion);

                json.Add("dependencies", dependenciesNodeObject);
            }
        }

        private void RemoveRuntimesNode(JObject json)
        {
            json.Remove("runtimes");
        }

        private bool PathsAreEqual(params string[] paths)
        {
            var normalizedPaths = paths.Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)).ToList();

            for (int i=1; i<normalizedPaths.Count(); ++i)
            {
                var path1 = normalizedPaths[i - 1];
                var path2 = normalizedPaths[i];
                if (!string.Equals(path1, path2, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}