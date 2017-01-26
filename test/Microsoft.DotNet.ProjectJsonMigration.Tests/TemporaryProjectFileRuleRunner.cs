using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.TestFramework;
using NuGet.Frameworks;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    internal class TemporaryProjectFileRuleRunner
    {
        public static ProjectRootElement RunRules(
            IEnumerable<IMigrationRule> rules,
            string projectJson,
            string testDirectory,
            ProjectRootElement xproj=null)
        {
            var projectContexts = GenerateProjectContextsFromString(testDirectory, projectJson);
            return RunMigrationRulesOnGeneratedProject(rules, projectContexts, testDirectory, xproj);
        }

        private static IEnumerable<ProjectContext> GenerateProjectContextsFromString(
            string projectDirectory,
            string json)
        {

            var globalJson = Path.Combine(new DirectoryInfo(projectDirectory).Parent.FullName, "global.json");
            if (!File.Exists(globalJson))
            {
                var file = new FileInfo(globalJson);
                File.WriteAllText(file.FullName, @"{}");
            }

            var testPj = new ProjectJsonBuilder(null)
                .FromStringBase(json)
                .SaveToDisk(projectDirectory);

            var projectContexts = ProjectContext.CreateContextForEachFramework(projectDirectory);

            if (projectContexts.Count() == 0)
            {
                projectContexts = new []
                { 
                    ProjectContext.Create(testPj, FrameworkConstants.CommonFrameworks.NetCoreApp10)
                };
            }

            return projectContexts;
        }

        private static ProjectRootElement RunMigrationRulesOnGeneratedProject(
            IEnumerable<IMigrationRule> rules,
            IEnumerable<ProjectContext> projectContexts,
            string testDirectory,
            ProjectRootElement xproj)
        {
            var project = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(testDirectory, testDirectory, project);
            var testInputs = new MigrationRuleInputs(
                projectContexts,
                project,
                project.AddItemGroup(),
                project.AddPropertyGroup(),
                xproj);

            foreach (var rule in rules)
            {
                rule.Apply(testSettings, testInputs);
            }

            return project;
        }
    }
}