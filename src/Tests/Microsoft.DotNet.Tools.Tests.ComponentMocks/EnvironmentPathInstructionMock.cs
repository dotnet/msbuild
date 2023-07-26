// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
