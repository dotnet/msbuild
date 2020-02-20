// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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
