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
    internal class CleanOutputProjectRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var outputProject = migrationRuleInputs.OutputMSBuildProject;

            CleanEmptyPropertiesAndItems(outputProject);
            CleanPropertiesThatDontChangeValue(outputProject);
            CleanEmptyPropertyAndItemGroups(outputProject);
            CleanEmptyTargets(outputProject);
        }

        private void CleanEmptyPropertyAndItemGroups(ProjectRootElement msbuildProject)
        {
            CleanEmptyProjectElementContainers(msbuildProject.PropertyGroups);
            CleanEmptyProjectElementContainers(msbuildProject.ItemGroups);
        }

        private void CleanEmptyPropertiesAndItems(ProjectRootElement msbuildProject)
        {
            foreach (var property in msbuildProject.Properties)
            {
                if (string.IsNullOrEmpty(property.Value))
                {
                    property.Parent.RemoveChild(property);
                }
            }

            foreach (var item in msbuildProject.Items)
            {
                if (string.IsNullOrEmpty(item.Include) && 
                    string.IsNullOrEmpty(item.Remove) &&
                    string.IsNullOrEmpty(item.Update))
                {
                    item.Parent.RemoveChild(item);
                }
            }
        }

        private void CleanPropertiesThatDontChangeValue(ProjectRootElement msbuildProject)
        {
            foreach (var property in msbuildProject.Properties)
            {
                var value = property.Value.Trim();
                var variableExpectedValue = "$(" + property.Name + ")";

                if (value == variableExpectedValue)
                {
                    property.Parent.RemoveChild(property);
                }
            }
        }

        private void CleanEmptyTargets(ProjectRootElement msbuildProject)
        {
            CleanEmptyProjectElementContainers(msbuildProject.Targets);
        }

        private void CleanEmptyProjectElementContainers(IEnumerable<ProjectElementContainer> containers)
        {
            foreach (var container in containers)
            {
                container.RemoveIfEmpty();
            }
        }
    }
}
