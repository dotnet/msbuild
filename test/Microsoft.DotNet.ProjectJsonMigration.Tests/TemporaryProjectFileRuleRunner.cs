using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class TemporaryProjectFileRuleRunner
    {
        public static ProjectRootElement RunRules(IEnumerable<IMigrationRule> rules, string projectJson,
            string testDirectory)
        {
            var projectContext = GenerateProjectContextFromString(testDirectory, projectJson);
            return RunMigrationRulesOnGeneratedProject(rules, projectContext, testDirectory);
        }

        private static ProjectContext GenerateProjectContextFromString(string projectDirectory, string json)
        {
            var testPj = new ProjectJsonBuilder(null)
                .FromStringBase(json)
                .SaveToDisk(projectDirectory);

            return ProjectContext.Create(testPj, FrameworkConstants.CommonFrameworks.NetCoreApp10);
        }

        private static ProjectRootElement RunMigrationRulesOnGeneratedProject(IEnumerable<IMigrationRule> rules,
            ProjectContext projectContext, string testDirectory)
        {
            var project = ProjectRootElement.Create();
            var testSettings = new MigrationSettings(testDirectory, testDirectory, "1.0.0", project);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, project,
                project.AddItemGroup(),
                project.AddPropertyGroup());

            foreach (var rule in rules)
            {
                rule.Apply(testSettings, testInputs);
            }

            return project;
        }
    }
}