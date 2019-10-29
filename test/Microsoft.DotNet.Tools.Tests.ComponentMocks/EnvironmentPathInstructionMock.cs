// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class EnvironmentPathInstructionMock : IEnvironmentPathInstruction
    {
        private readonly string _packageExecutablePath;
        private readonly bool _packageExecutablePathExists;
        private readonly IReporter _reporter;
        public const string MockInstructionText = "MOCK INSTRUCTION";

        public EnvironmentPathInstructionMock(
            IReporter reporter,
            string packageExecutablePath,
            bool packageExecutablePathExists = false)
        {
            _packageExecutablePath =
                packageExecutablePath ?? throw new ArgumentNullException(nameof(packageExecutablePath));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            _packageExecutablePathExists = packageExecutablePathExists;
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExists())
            {
                _reporter.WriteLine(MockInstructionText);
            }
        }

        private bool PackageExecutablePathExists()
        {
            return _packageExecutablePathExists;
        }
    }
}
