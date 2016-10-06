// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.Build.Construction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateJsonPropertiesRule : IMigrationRule
    {
        private Dictionary<string, ConditionalTransform<JToken, ProjectPropertyElement>> _propertyMappings
            = new Dictionary<string, ConditionalTransform<JToken, ProjectPropertyElement>>
            {
                ["userSecretsId"] = new AddPropertyTransform<JToken>("UserSecretsId",
                            j => j.Value<string>(),
                            j => !string.IsNullOrEmpty(j.Value<string>()))
            };

        private readonly ITransformApplicator _transformApplicator;

        public MigrateJsonPropertiesRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var propertyGroup = migrationRuleInputs.CommonPropertyGroup;

            var projectFile = migrationRuleInputs.DefaultProjectContext.ProjectFile.ProjectFilePath;

            using (var stream = new FileStream(projectFile, FileMode.Open))
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var rawProject = JObject.Load(jsonReader);
                foreach (var prop in _propertyMappings)
                {
                    var token = rawProject.GetValue(prop.Key);
                    if (token != null)
                    {
                        _transformApplicator.Execute(prop.Value.Transform(token), propertyGroup);
                    }
                }
            }
        }
    }
}
