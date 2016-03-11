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
                var assemblyUnderTest = _projectContext.GetOutputPaths(
                    _dotentTestParams.Config,
                    outputPath: _dotentTestParams.Output).CompilationFiles.Assembly;

                if (!string.IsNullOrEmpty(_dotentTestParams.Output))
                {
                    assemblyUnderTest = _projectContext.GetOutputPaths(
                        _dotentTestParams.Config,
                        outputPath: _dotentTestParams.Output).RuntimeFiles.Assembly;
                }

                return assemblyUnderTest;
            }
        }
    }
}
