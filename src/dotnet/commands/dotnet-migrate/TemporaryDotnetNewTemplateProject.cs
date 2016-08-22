using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli
{
    internal class TemporaryDotnetNewTemplateProject
    {
        private static string s_temporaryDotnetNewMSBuildProjectName = "p";

        public TemporaryDotnetNewTemplateProject()
        {
            ProjectDirectory = CreateDotnetNewMSBuild(s_temporaryDotnetNewMSBuildProjectName);
            MSBuildProject = GetMSBuildProject(ProjectDirectory);
            ProjectJson = GetProjectJson(ProjectDirectory);
        }

        public ProjectRootElement MSBuildProject { get; }
        public JObject ProjectJson { get; }
        public string ProjectDirectory { get; }

        public string ProjectJsonPath => Path.Combine(ProjectDirectory, "project.json");
        public string MSBuildProjectPath => Path.Combine(ProjectDirectory, s_temporaryDotnetNewMSBuildProjectName);

        public void Clean()
        {
            Directory.Delete(ProjectDirectory, true);
        }

        private string CreateDotnetNewMSBuild(string projectName)
        {
            var guid = Guid.NewGuid().ToString();
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                this.GetType().Namespace,
                guid,
                s_temporaryDotnetNewMSBuildProjectName);

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            RunCommand("new", new string[] { "-t", "msbuild" }, tempDir);

            return tempDir;
        }

        private ProjectRootElement GetMSBuildProject(string temporaryDotnetNewMSBuildDirectory)
        {
            var templateProjPath = Path.Combine(temporaryDotnetNewMSBuildDirectory,
                s_temporaryDotnetNewMSBuildProjectName + ".csproj");

            return ProjectRootElement.Open(templateProjPath);
        }

        private JObject GetProjectJson(string temporaryDotnetNewMSBuildDirectory)
        {
            var projectJsonFile = Path.Combine(temporaryDotnetNewMSBuildDirectory, "project.json");
            return JObject.Parse(File.ReadAllText(projectJsonFile));
        }

        private void RunCommand(string commandToExecute, IEnumerable<string> args, string workingDirectory)
        {
            var command = new DotNetCommandFactory()
                .Create(commandToExecute, args)
                .WorkingDirectory(workingDirectory)
                .CaptureStdOut()
                .CaptureStdErr();

            var commandResult = command.Execute();

            if (commandResult.ExitCode != 0)
            {
                throw new Exception($"Failed to run {commandToExecute} in directory: {workingDirectory}");
            }
        }
    }
}
