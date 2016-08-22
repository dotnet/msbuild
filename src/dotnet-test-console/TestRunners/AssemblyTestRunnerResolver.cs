// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class AssemblyTestRunnerResolver : ITestRunnerResolver
    {
        private readonly string _assemblyUnderTestPath;

        private readonly IDirectory _directory;

        public AssemblyTestRunnerResolver(string assemblyUnderTestPath) :
            this(assemblyUnderTestPath, FileSystemWrapper.Default.Directory)
        {
        }

        internal AssemblyTestRunnerResolver(string assemblyUnderTestPath, IDirectory directory)
        {
            _assemblyUnderTestPath = assemblyUnderTestPath;
            _directory = directory;
        }

        public string ResolveTestRunner()
        {
            var testRunnerPath = _directory.GetFiles(_assemblyUnderTestPath, "dotnet-test-*").FirstOrDefault();

            return Path.GetFileNameWithoutExtension(testRunnerPath);
        }
    }
}
