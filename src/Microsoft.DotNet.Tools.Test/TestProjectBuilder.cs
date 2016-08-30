// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestProjectBuilder
    {
        public int BuildTestProject(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            return dotnetTestParams.NoBuild ? 0 : DoBuildTestProject(projectContext, dotnetTestParams);
        }

        private int DoBuildTestProject(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            var strings = new List<string>
            {
                $"{dotnetTestParams.ProjectOrAssemblyPath}",
                $"--configuration", dotnetTestParams.Config,
                "--framework", projectContext.TargetFramework.ToString()
            };

            // Build the test specifically for the target framework \ rid of the ProjectContext.
            // This avoids building the project for tfms that the user did not request.

            if (!string.IsNullOrEmpty(dotnetTestParams.BuildBasePath))
            {
                strings.Add("--build-base-path");
                strings.Add(dotnetTestParams.BuildBasePath);
            }

            if (!string.IsNullOrEmpty(dotnetTestParams.Output))
            {
                strings.Add("--output");
                strings.Add(dotnetTestParams.Output);
            }

            if (!string.IsNullOrEmpty(projectContext.RuntimeIdentifier))
            {
                strings.Add("--runtime");
                strings.Add(projectContext.RuntimeIdentifier);
            }

            var result = Command.CreateDotNet("build", strings).Execute().ExitCode;

            return result;
        }
    }
}