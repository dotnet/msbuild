// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public abstract class BaseDotnetTestRunner : IDotnetTestRunner
    {
        public int RunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            var result = BuildTestProject(dotnetTestParams);

            return result == 0 ? DoRunTests(projectContext, dotnetTestParams) : result;
        }

        internal abstract int DoRunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams);

        private int BuildTestProject(DotnetTestParams dotnetTestParams)
        {
            if (dotnetTestParams.NoBuild)
            {
                return 0;
            }

            return DoBuildTestProject(dotnetTestParams);
        }

        private int DoBuildTestProject(DotnetTestParams dotnetTestParams)
        {
            var strings = new List<string>
            {
                $"--configuration",
                dotnetTestParams.Config,
                $"{dotnetTestParams.ProjectPath}"
            };

            if (dotnetTestParams.Framework != null)
            {
                strings.Add("--framework");
                strings.Add($"{dotnetTestParams.Framework}");
            }

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

            if (!string.IsNullOrEmpty(dotnetTestParams.Runtime))
            {
                strings.Add("--runtime");
                strings.Add(dotnetTestParams.Runtime);
            }

            var result = Build.BuildCommand.Run(strings.ToArray());

            return result;
        }
    }
}
