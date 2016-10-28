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
    internal class RemoveDefaultsFromProjectRule : IMigrationRule
    {
        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            foreach (var element in 
                migrationRuleInputs.OutputMSBuildProject.Children
                    .Where(c => c.Label == AddDefaultsToProjectRule.c_DefaultsProjectElementContainerLabel))
            {
                element.Parent.RemoveChild(element);
            }
        }
    }
}
