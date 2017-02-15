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
using Microsoft.DotNet.Internal.ProjectModel;
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
        public void RuntimeOptionsAreCopiedFromProjectJsonToRuntimeConfigTemplateJsonFile()
        {
            var testInstance = TestAssets.Get("TestAppWithRuntimeOptions")
                                        .CreateInstance()
                                        .WithSourceFiles()
                                        .Root;

            var projectDir = testInstance.FullName;
            var projectPath = Path.Combine(projectDir, "project.json");

            var project = JObject.Parse(File.ReadAllText(projectPath));
            var rawRuntimeOptions = (JObject)project.GetValue("runtimeOptions");

            var projectContext = ProjectContext.Create(projectDir, FrameworkConstants.CommonFrameworks.NetCoreApp10);

            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(projectDir, projectDir, default(ProjectRootElement));
            var testInputs = new MigrationRuleInputs(new[] { projectContext }, null, null, null);
            new MigrateRuntimeOptionsRule().Apply(testSettings, testInputs);

            var migratedRuntimeOptionsPath = Path.Combine(projectDir, s_runtimeConfigFileName);

            File.Exists(migratedRuntimeOptionsPath).Should().BeTrue();

            var migratedRuntimeOptionsContent = JObject.Parse(File.ReadAllText(migratedRuntimeOptionsPath));
            JToken.DeepEquals(rawRuntimeOptions, migratedRuntimeOptionsContent).Should().BeTrue();
        }

        [Fact]
        public void MigratingProjectJsonWithNoRuntimeOptionsProducesNoRuntimeConfigTemplateJsonFile()
        {
            var testInstance = TestAssets.Get("PJTestAppSimple")
                            .CreateInstance()
                            .WithSourceFiles()
                            .Root;

            var projectDir = testInstance.FullName;

            var projectContext = ProjectContext.Create(projectDir, FrameworkConstants.CommonFrameworks.NetCoreApp10);

            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(projectDir, projectDir, default(ProjectRootElement));
            var testInputs = new MigrationRuleInputs(new[] { projectContext }, null, null, null);
            new MigrateRuntimeOptionsRule().Apply(testSettings, testInputs);

            var migratedRuntimeOptionsPath = Path.Combine(projectDir, s_runtimeConfigFileName);

            File.Exists(migratedRuntimeOptionsPath).Should().BeFalse();
        }

        [Fact]
        public void MigratingProjectJsonWithOnlyServerGCRuntimeOptionsProducesNoRuntimeConfigTemplateJsonFile()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": true
                    }
                  }
                }";

            RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var migratedRuntimeOptionsPath = Path.Combine(testDirectory, s_runtimeConfigFileName);
            File.Exists(migratedRuntimeOptionsPath).Should().BeFalse();
        }

        [Fact]
        public void MigratingProjectJsonWithServerGCAndOtherConfigPropertiesProducesRuntimeConfigTemplateJsonFile()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": false,
                      ""Other"": false
                    }
                  }
                }";

            RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var migratedRuntimeOptionsPath = Path.Combine(testDirectory, s_runtimeConfigFileName);
            File.Exists(migratedRuntimeOptionsPath).Should().BeTrue();

            var root = JObject.Parse(File.ReadAllText(migratedRuntimeOptionsPath));
            var configProperties = root.Value<JObject>("configProperties");
            configProperties.Should().NotBeNull();
            configProperties["System.GC.Server"].Should().BeNull();
            configProperties["Other"].Should().NotBeNull();
        }

        [Fact]
        public void MigratingProjectJsonWithServerGCAndOtherRuntimeOptionsProducesRuntimeConfigTemplateJsonFile()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": false
                    },
                    ""Other"": false
                  }
                }";

            RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var migratedRuntimeOptionsPath = Path.Combine(testDirectory, s_runtimeConfigFileName);
            File.Exists(migratedRuntimeOptionsPath).Should().BeTrue();

            var root = JObject.Parse(File.ReadAllText(migratedRuntimeOptionsPath));
            root.Value<JObject>("configProperties").Should().BeNull();
        }

        [Fact]
        public void MigratingProjectJsonWithServerGCTrueProducesServerGarbageCollectionProperty()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": true
                    }
                  }
                }";

            var mockProj = RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var props = mockProj.Properties.Where(p => p.Name.Equals("ServerGarbageCollection", StringComparison.Ordinal));
            props.Count().Should().Be(1);
            props.First().Value.Should().Be("true");
        }

        [Fact]
        public void MigratingProjectJsonWithServerGCFalseProducesServerGarbageCollectionProperty()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": false
                    }
                  }
                }";

            var mockProj = RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var props = mockProj.Properties.Where(p => p.Name.Equals("ServerGarbageCollection", StringComparison.Ordinal));
            props.Count().Should().Be(1);
            props.First().Value.Should().Be("false");
        }

        [Fact]
        public void MigratingWebProjectJsonWithServerGCTrueDoesNotProduceServerGarbageCollectionProperty()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""buildOptions"": {
                    ""emitEntryPoint"": true
                  },
                  ""dependencies"": {
                    ""Microsoft.AspNetCore.Mvc"": ""1.0.0""
                  },
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": true
                    }
                  }
                }";

            var mockProj = RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var props = mockProj.Properties.Where(p => p.Name.Equals("ServerGarbageCollection", StringComparison.Ordinal));
            props.Count().Should().Be(0);
        }

        [Fact]
        public void MigratingWebProjectJsonWithServerGCFalseProducesServerGarbageCollectionProperty()
        {
            var testDirectory = Temp.CreateDirectory().Path;

            var pj = @"
                {
                  ""buildOptions"": {
                    ""emitEntryPoint"": true
                  },
                  ""dependencies"": {
                    ""Microsoft.AspNetCore.Mvc"": ""1.0.0""
                  },
                  ""runtimeOptions"": {
                    ""configProperties"": {
                      ""System.GC.Server"": false
                    }
                  }
                }";

            var mockProj = RunMigrateRuntimeOptionsRulePj(pj, testDirectory);
            var props = mockProj.Properties.Where(p => p.Name.Equals("ServerGarbageCollection", StringComparison.Ordinal));
            props.Count().Should().Be(1);
            props.First().Value.Should().Be("false");
        }

        private ProjectRootElement RunMigrateRuntimeOptionsRulePj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigrateRuntimeOptionsRule()
            }, s, testDirectory);
        }
    }
}
