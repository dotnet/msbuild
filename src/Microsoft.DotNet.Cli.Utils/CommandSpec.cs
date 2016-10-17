using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandSpec
    {
        public CommandSpec(
            string path,
            string args,
            CommandResolutionStrategy resolutionStrategy,
            Dictionary<string, string> environmentVariables = null)
        {
            Path = path;
            Args = args;
            ResolutionStrategy = resolutionStrategy;
            EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>();
        }

        public string Path { get; }

        public string Args { get; }

        public Dictionary<string, string> EnvironmentVariables { get; }

        public CommandResolutionStrategy ResolutionStrategy { get; }

        internal void AddEnvironmentVariablesFromProject(IProject project)
        {
            foreach (var environmentVariable in project.EnvironmentVariables)
            {
                EnvironmentVariables.Add(environmentVariable.Key, environmentVariable.Value);
            }
        }
    }
}