// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MigrateCommand;
using Microsoft.DotNet.Tools.Sln;

namespace Microsoft.DotNet.Tools.Migrate
{
    public class DotnetSlnRedirector : ICanManipulateSolutionFile
    {
        public void AddProjectToSolution(string solutionFilePath, string projectFilePath)
        {
            var newCommandArgs = new[] {solutionFilePath, SlnAddParser.SlnAdd().Name, projectFilePath};
            var result = SlnCommand.Run(newCommandArgs);

            if (result != 0)
            {
                throw new GracefulException(
                    $"{nameof(SlnCommand)} " +
                    "failed to execute given args " +
                    $"{string.Join(" ", newCommandArgs)}");
            }
        }

        public void RemoveProjectFromSolution(string solutionFilePath, string projectFilePath)
        {
            var newCommandArgs = new[] {solutionFilePath, SlnRemoveParser.SlnRemove().Name, projectFilePath};
            var result = SlnCommand.Run(newCommandArgs);

            if (result != 0)
            {
                throw new GracefulException(
                    $"{nameof(SlnCommand)} " +
                    "failed to execute given args " +
                    $"{string.Join(" ", newCommandArgs)}");
            }
        }
    }
}