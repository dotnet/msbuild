// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class AssemblyTestRunnerNameResolver : ITestRunnerNameResolver
    {
        private readonly string _directoryOfAssemblyUnderTest;

        private readonly IDirectory _directory;

        public AssemblyTestRunnerNameResolver(string assemblyUnderTest) :
            this(assemblyUnderTest, FileSystemWrapper.Default.Directory)
        {
        }

        internal AssemblyTestRunnerNameResolver(string assemblyUnderTest, IDirectory directory)
        {
            _directoryOfAssemblyUnderTest = directory.GetDirectoryFullName(assemblyUnderTest);
            _directory = directory;
        }

        public string ResolveTestRunner()
        {
            var testRunnerPath = _directory.GetFiles(_directoryOfAssemblyUnderTest, "dotnet-test-*").FirstOrDefault();

            return Path.GetFileNameWithoutExtension(testRunnerPath);
        }
    }
}
