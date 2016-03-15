// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Test
{
    public class AssemblyUnderTest
    {
        private readonly ProjectContext _projectContext;
        private readonly DotnetTestParams _dotentTestParams;

        public AssemblyUnderTest(ProjectContext projectContext, DotnetTestParams dotentTestParams)
        {
            _projectContext = projectContext;
            _dotentTestParams = dotentTestParams;
        }

        public string Path
        {
            get
            {
                var outputPaths = _projectContext.GetOutputPaths(
                    _dotentTestParams.Config,
                    _dotentTestParams.BuildBasePath,
                    _dotentTestParams.Output);

                var assemblyUnderTest = outputPaths.CompilationFiles.Assembly;

                if (!string.IsNullOrEmpty(_dotentTestParams.Output) ||
                    !string.IsNullOrEmpty(_dotentTestParams.BuildBasePath))
                {
                    assemblyUnderTest = outputPaths.RuntimeFiles.Assembly;
                }

                return assemblyUnderTest;
            }
        }
    }
}
