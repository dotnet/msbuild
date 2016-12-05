using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Internal.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    internal class TemporaryProjectFileRuleRunner
    {
        public static ProjectRootElement RunRules(IEnumerable<IMigrationRule> rules, string projectJson,
            string testDirectory, ProjectRootElement xproj=null)
        {
            var projectContext = GenerateProjectContextFromString(testDirectory, projectJson);
            return RunMigrationRulesOnGeneratedProject(rules, projectContext, testDirectory, xproj);
        }

        private static ProjectContext GenerateProjectContextFromString(string projectDirectory, string json)
        {
            var testPj = new ProjectJsonBuilder(null)
                .FromStringBase(json)
                .SaveToDisk(projectDirectory);

            return ProjectContext.Create(testPj, FrameworkConstants.CommonFrameworks.NetCoreApp10);
        }

        private static ProjectRootElement RunMigrationRulesOnGeneratedProject(IEnumerable<IMigrationRule> rules,
            ProjectContext projectContext, string testDirectory, ProjectRootElement xproj)
        {
            var project = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(testDirectory, testDirectory, project);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, project,
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