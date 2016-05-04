// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatIWantANewCSApp : TestBase
    {
        [Fact]
        public void When_NewtonsoftJson_dependency_added_Then_project_restores_and_runs()
        {
            var rootPath = Temp.CreateDirectory().Path;
            var projectJsonFile = Path.Combine(rootPath, "project.json");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new");
            
            AddProjectJsonDependency(projectJsonFile, "Newtonsoft.Json", "7.0.1");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore")
                .Should().Pass();

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("run")
                .Should().Pass();
        }
        
        [Fact]
        public void When_dotnet_build_is_invoked_Then_project_builds_without_warnings()
        {
            var rootPath = Temp.CreateDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore");

            var buildResult = new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .ExecuteWithCapturedOutput("build");
            
            buildResult.Should().Pass();
            buildResult.Should().NotHaveStdErr();
        }
        
        private static void AddProjectJsonDependency(string projectJsonPath, string dependencyId, string dependencyVersion)
        {
            var projectJsonRoot = ReadProject(projectJsonPath);

            var dependenciesNode = projectJsonRoot
                .Descendants()
                .OfType<JProperty>()
                .First(p => p.Name == "dependencies");

            ((JObject)dependenciesNode.Value).Add(new JProperty(dependencyId, dependencyVersion));

            WriteProject(projectJsonRoot, projectJsonPath);
        }

        private static JObject ReadProject(string projectJsonPath)
        {
            using (TextReader projectFileReader = File.OpenText(projectJsonPath))
            {
                var projectJsonReader = new JsonTextReader(projectFileReader);

                var serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(projectJsonReader);
            }
        }

        private static void WriteProject(JObject projectRoot, string projectJsonPath)
        {
            string projectJson = JsonConvert.SerializeObject(projectRoot, Formatting.Indented);

            File.WriteAllText(projectJsonPath, projectJson + Environment.NewLine);
        }
    }
}
