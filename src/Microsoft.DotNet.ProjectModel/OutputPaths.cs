// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPaths
    {
        private readonly string _runtimePath;
        private readonly RuntimeOutputFiles _runtimeFiles;

        public OutputPaths(string intermediateOutputDirectoryPath,
            string compilationOutputPath,
            string runtimePath,
            CompilationOutputFiles compilationFiles,
            RuntimeOutputFiles runtimeFiles)
        {
            _runtimePath = runtimePath;
            _runtimeFiles = runtimeFiles;
            CompilationOutputPath = compilationOutputPath;
            IntermediateOutputDirectoryPath = intermediateOutputDirectoryPath;
            CompilationFiles = compilationFiles;
        }

        public string CompilationOutputPath { get; }

        public string IntermediateOutputDirectoryPath { get; }

        public string RuntimeOutputPath
        {
            get
            {
                if (_runtimePath == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot get runtime output path for {nameof(OutputPaths)} with no runtime set");
                }
                return _runtimePath;
            }
        }

        public CompilationOutputFiles CompilationFiles { get; }

        public RuntimeOutputFiles RuntimeFiles
        {
            get
            {
                if (_runtimeFiles == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot get runtime output files for {nameof(OutputPaths)} with no runtime set");
                }
                return _runtimeFiles;
            }
        }
    }
}
