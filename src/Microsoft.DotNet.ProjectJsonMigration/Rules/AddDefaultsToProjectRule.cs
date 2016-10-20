// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.Build.Construction;
using System.Collections.Generic;
using Microsoft.DotNet.ProjectJsonMigration.Models;
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class AddDefaultsToProjectRule : IMigrationRule
    {
        internal const string c_DefaultsProjectElementContainerLabel = "MigrationDefaultsTempContainer";
        internal const string c_SdkDefaultsJsonFileName = "sdkdefaults.json";

        private readonly ITransformApplicator _transformApplicator;

        public AddDefaultsToProjectRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            SerializableMigrationDefaultsInfo defaults = ResolveDefaults(migrationSettings);

            var project = migrationRuleInputs.OutputMSBuildProject;
            var defaultsPropertyGroups = new Dictionary<string, ProjectPropertyGroupElement>();
            var defaultsItemGroups = new Dictionary<string, ProjectItemGroupElement>();

            AddDefaultPropertiesToProject(defaults, project, defaultsPropertyGroups);
            AddDefaultItemsToProject(defaults, project, defaultsItemGroups);
        }

        private void AddDefaultItemsToProject(
            SerializableMigrationDefaultsInfo defaults, 
            ProjectRootElement project, 
            Dictionary<string, ProjectItemGroupElement> defaultsItemGroups)
        {
            foreach (var itemInfo in defaults.Items)
            {
                ProjectItemGroupElement itemGroup;
                var parentCondition = itemInfo.ParentCondition ?? string.Empty;

                if (!defaultsItemGroups.TryGetValue(parentCondition, out itemGroup))
                {
                    itemGroup = project.AddItemGroup();
                    itemGroup.Label = c_DefaultsProjectElementContainerLabel;
                    itemGroup.Condition = parentCondition;

                    defaultsItemGroups[parentCondition] = itemGroup;
                }

                var item = itemGroup.AddItem(itemInfo.ItemType, itemInfo.Include);
                item.Exclude = itemInfo.Exclude;
                item.Remove = itemInfo.Remove;
                item.Condition = itemInfo.Condition;
            }
        }

        private static void AddDefaultPropertiesToProject(
            SerializableMigrationDefaultsInfo defaults, 
            ProjectRootElement project, 
            Dictionary<string, ProjectPropertyGroupElement> defaultsPropertyGroups)
        {
            foreach (var propertyInfo in defaults.Properties)
            {
                ProjectPropertyGroupElement propertyGroup;
                var parentCondition = propertyInfo.ParentCondition ?? string.Empty;

                if (!defaultsPropertyGroups.TryGetValue(parentCondition, out propertyGroup))
                {
                    propertyGroup = project.AddPropertyGroup();
                    propertyGroup.Label = c_DefaultsProjectElementContainerLabel;
                    propertyGroup.Condition = parentCondition;

                    defaultsPropertyGroups[parentCondition] = propertyGroup;
                }

                var property = propertyGroup.AddProperty(propertyInfo.Name, propertyInfo.Value);
                property.Condition = propertyInfo.Condition;
            }
        }

        private SerializableMigrationDefaultsInfo ResolveDefaults(MigrationSettings migrationSettings)
        {
            var sdkDefaultFile = TryResolveSdkDefaultsFileFromSettings(migrationSettings);
            if (sdkDefaultFile != null)
            {
                return DeserializeDefaults(File.ReadAllText(sdkDefaultFile));
            }

            var thisAssembly = typeof(AddDefaultsToProjectRule).GetTypeInfo().Assembly;
            using (var resource = thisAssembly.GetManifestResourceStream("Microsoft.DotNet.ProjectJsonMigration." + c_SdkDefaultsJsonFileName))
            {
                using (StreamReader reader = new StreamReader(resource))
                {
                    return DeserializeDefaults(reader.ReadToEnd());
                }
            }
        }

        private string TryResolveSdkDefaultsFileFromSettings(MigrationSettings migrationSettings)
        {
            var candidate = migrationSettings.SdkDefaultsFilePath;
            if (candidate == null)
            {
                return null;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
            return null;
        }

        private SerializableMigrationDefaultsInfo DeserializeDefaults(string sdkDefaultJson)
        {
            return JsonConvert.DeserializeObject<SerializableMigrationDefaultsInfo>(sdkDefaultJson);
        }
    }
}
