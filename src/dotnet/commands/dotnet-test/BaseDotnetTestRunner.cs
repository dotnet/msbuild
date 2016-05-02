// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public abstract class BaseDotnetTestRunner : IDotnetTestRunner
    {
        public int RunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams, BuildWorkspace workspace)
        {
            var result = BuildTestProject(projectContext, dotnetTestParams, workspace);

            return result == 0 ? DoRunTests(projectContext, dotnetTestParams) : result;
        }

        internal abstract int DoRunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams);

        private int BuildTestProject(ProjectContext projectContext, DotnetTestParams dotnetTestParams, BuildWorkspace workspace)
        {
            if (dotnetTestParams.NoBuild)
            {
                return 0;
            }

            return DoBuildTestProject(projectContext, dotnetTestParams, workspace);
        }

        private int DoBuildTestProject(ProjectContext projectContext, DotnetTestParams dotnetTestParams, BuildWorkspace workspace)
        {
            var strings = new List<string>
            {
                $"--configuration",
                dotnetTestParams.Config,
                $"{dotnetTestParams.ProjectPath}"
            };

            // Build the test specifically for the target framework \ rid of the ProjectContext. This avoids building the project
            // for tfms that the user did not request.
            strings.Add("--framework");
            strings.Add(projectContext.TargetFramework.ToString());

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

            var result = Build.BuildCommand.Run(strings.ToArray(), workspace);

            return result;
        }
    }
}
