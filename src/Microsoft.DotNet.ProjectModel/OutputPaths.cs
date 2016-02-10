// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPaths
    {
        private string _compilationPath;
        private string _intermediatePath;
        private string _runtimePath;
        private readonly RuntimeOutputFiles _runtimeFiles;

        public OutputPaths(string intermediateOutputDirectoryPath,
            string compilationOutputPath,
            string runtimePath,
            CompilationOutputFiles compilationFiles,
            RuntimeOutputFiles runtimeFiles)
        {
            RuntimeOutputPath = runtimePath;
            _runtimeFiles = runtimeFiles;
            CompilationOutputPath = compilationOutputPath;
            IntermediateOutputDirectoryPath = intermediateOutputDirectoryPath;
            CompilationFiles = compilationFiles;
        }

        public string CompilationOutputPath
        {
            get
            {
                return _compilationPath;
            }
            private set
            {
                _compilationPath = value?.TrimEnd('\\');
            }
        }

        public string IntermediateOutputDirectoryPath
        {
            get
            {
                return _intermediatePath;
            }
            private set
            {
                _intermediatePath = value?.TrimEnd('\\');
            }
        }

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
            private set
            {
                _runtimePath = value?.TrimEnd('\\');
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
