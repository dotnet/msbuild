// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateRuntimeOptionsRule : IMigrationRule
    {
        private const string ConfigPropertiesTokenName = "configProperties";
        private const string SystemGCServerTokenName = "System.GC.Server";
        private readonly ITransformApplicator _transformApplicator;
        private static readonly string s_runtimeOptionsFileName = "runtimeconfig.template.json";

        public MigrateRuntimeOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var raw = projectContext.ProjectFile.RawRuntimeOptions;
            var outputRuntimeOptionsFile = Path.Combine(migrationSettings.OutputDirectory, s_runtimeOptionsFileName);

            if (!string.IsNullOrEmpty(raw))
            {
                if (File.Exists(outputRuntimeOptionsFile))
                {
                    MigrationErrorCodes.MIGRATE1015(
                        String.Format(LocalizableStrings.ProjAlreadyExistsError, outputRuntimeOptionsFile)).Throw();
                }

                var runtimeOptions = JObject.Parse(raw);
                if (HasServerGCProperty(runtimeOptions))
                {
                    bool serverGCValue = GetServerGCValue(runtimeOptions);

                    if (!IsServerGCValueInjectedBySdk(serverGCValue, projectContext.ProjectFile.GetProjectType()))
                    {
                        var propertyTransform = new AddPropertyTransform<bool>(
                            "ServerGarbageCollection",
                            gcValue => gcValue.ToString().ToLower(),
                            gcValue => true);

                        _transformApplicator.Execute(
                            propertyTransform.Transform(serverGCValue),
                            migrationRuleInputs.CommonPropertyGroup,
                            true);
                    }

                    RemoveServerGCProperty(runtimeOptions);
                }

                if (runtimeOptions.HasValues)
                {
                    File.WriteAllText(outputRuntimeOptionsFile, runtimeOptions.ToString());
                }
            }
        }

        private bool IsServerGCValueInjectedBySdk(bool serverGCValue, ProjectType projectType)
        {
            return (projectType == ProjectType.Web && serverGCValue);
        }

        private bool HasServerGCProperty(JObject runtimeOptions)
        {
            bool hasServerGCProperty = false;

            var configProperties = runtimeOptions.Value<JObject>(ConfigPropertiesTokenName);
            if (configProperties != null)
            {
                hasServerGCProperty = configProperties[SystemGCServerTokenName] != null;
            }

            return hasServerGCProperty;
        }

        private bool GetServerGCValue(JObject runtimeOptions)
        {
            var configProperties = runtimeOptions[ConfigPropertiesTokenName];
            return configProperties.Value<bool>(SystemGCServerTokenName);
        }

        private void RemoveServerGCProperty(JObject runtimeOptions)
        {
            var configProperties = runtimeOptions.Value<JObject>(ConfigPropertiesTokenName);
            if (configProperties != null)
            {
                configProperties.Remove(SystemGCServerTokenName);
                if (!configProperties.HasValues)
                {
                    runtimeOptions.Remove(ConfigPropertiesTokenName);
                }
            }
        }
    }
}
