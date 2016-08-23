// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class AssemblyTestRunnerResolver : ITestRunnerResolver
    {
        private readonly string _directoryOfAssemblyUnderTest;

        private readonly IDirectory _directory;

        public AssemblyTestRunnerResolver(string directoryOfAssemblyUnderTest) :
            this(directoryOfAssemblyUnderTest, FileSystemWrapper.Default.Directory)
        {
        }

        internal AssemblyTestRunnerResolver(string directoryOfAssemblyUnderTest, IDirectory directory)
        {
            _directoryOfAssemblyUnderTest = Path.GetDirectoryName(directoryOfAssemblyUnderTest);
            _directory = directory;
        }

        public string ResolveTestRunner()
        {
            var testRunnerPath = _directory.GetFiles(_directoryOfAssemblyUnderTest, "dotnet-test-*").FirstOrDefault();

            return Path.GetFileNameWithoutExtension(testRunnerPath);
        }
    }
}
