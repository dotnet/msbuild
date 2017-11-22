// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ToolPackageObtainer;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli
{
    internal class ProjectRestorer : IProjectRestorer
    {
        public void Restore(
            FilePath projectPath,
            DirectoryPath assetJsonOutput,
            FilePath? nugetconfig)
        {
            var argsToPassToRestore = new List<string>();

            argsToPassToRestore.Add(projectPath.ToQuotedString());
            if (nugetconfig != null)
            {
                argsToPassToRestore.Add("--configfile");
                argsToPassToRestore.Add(nugetconfig.Value.ToQuotedString());
            }

            argsToPassToRestore.AddRange(new List<string>
            {
                "--runtime",
                RuntimeEnvironment.GetRuntimeIdentifier(),
                $"/p:BaseIntermediateOutputPath={assetJsonOutput.ToQuotedString()}"
            });

            var command = new DotNetCommandFactory(alwaysRunOutOfProc: true)
                .Create(
                    "restore",
                    argsToPassToRestore)
                .CaptureStdOut()
                .CaptureStdErr();

            var result = command.Execute();
            if (result.ExitCode != 0)
            {
                throw new PackageObtainException("Failed to restore package. " +
                                                 $"{Environment.NewLine}WorkingDirectory: " +
                                                 result.StartInfo.WorkingDirectory +
                                                 $"{Environment.NewLine}Arguments: " +
                                                 result.StartInfo.Arguments +
                                                 $"{Environment.NewLine}Output: " +
                                                 result.StdErr + result.StdOut);
            }
        }
    }
}
