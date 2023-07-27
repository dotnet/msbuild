// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.CommandFactory
{
    public class CommandSpec
    {
        public CommandSpec(
            string path,
            string args,
            Dictionary<string, string> environmentVariables = null)
        {
            Path = path;
            Args = args;
            EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>();
        }

        public string Path { get; }

        public string Args { get; }

        public Dictionary<string, string> EnvironmentVariables { get; }

        internal void AddEnvironmentVariablesFromProject(IProject project)
        {
            foreach (var environmentVariable in project.EnvironmentVariables)
            {
                EnvironmentVariables.Add(environmentVariable.Key, environmentVariable.Value);
            }
        }
    }
}
