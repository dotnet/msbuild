// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateRuntimeOptions : TestBase
    {
        private static readonly string s_runtimeConfigFileName = "runtimeconfig.template.json";

        [Fact]
        public void RuntimeOptions_are_copied_from_projectJson_to_runtimeconfig_template_json_file()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithRuntimeOptions");
            var projectDir = testInstance.Path;
            var projectPath = Path.Combine(testInstance.Path, "project.json");

            var project = JObject.Parse(File.ReadAllText(projectPath));
            var rawRuntimeOptions = (JObject)project.GetValue("runtimeOptions");

            var projectContext = ProjectContext.Create(projectDir, FrameworkConstants.CommonFrameworks.NetCoreApp10);

            var testSettings = new MigrationSettings(projectDir, projectDir, "1.0.0", default(ProjectRootElement));
            var testInputs = new MigrationRuleInputs(new[] { projectContext }, null, null, null);
            new MigrateRuntimeOptionsRule().Apply(testSettings, testInputs);

            var migratedRuntimeOptionsPath = Path.Combine(projectDir, s_runtimeConfigFileName);

            File.Exists(migratedRuntimeOptionsPath).Should().BeTrue();

            var migratedRuntimeOptionsContent = JObject.Parse(File.ReadAllText(migratedRuntimeOptionsPath));
            JToken.DeepEquals(rawRuntimeOptions, migratedRuntimeOptionsContent).Should().BeTrue();
        }

        [Fact]
        public void Migrating_ProjectJson_with_no_RuntimeOptions_produces_no_runtimeconfig_template_json_file()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppSimple");
            var projectDir = testInstance.Path;

            var projectContext = ProjectContext.Create(projectDir, FrameworkConstants.CommonFrameworks.NetCoreApp10);

            var testSettings = new MigrationSettings(projectDir, projectDir, "1.0.0", default(ProjectRootElement));
            var testInputs = new MigrationRuleInputs(new[] { projectContext }, null, null, null);
            new MigrateRuntimeOptionsRule().Apply(testSettings, testInputs);

            var migratedRuntimeOptionsPath = Path.Combine(projectDir, s_runtimeConfigFileName);

            File.Exists(migratedRuntimeOptionsPath).Should().BeFalse();
        }
    }
}
