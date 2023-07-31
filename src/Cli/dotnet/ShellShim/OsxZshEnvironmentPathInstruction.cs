// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.ShellShim
{
    internal class OsxZshEnvironmentPathInstruction : IEnvironmentPathInstruction
    {
        private const string PathName = "PATH";
        private readonly BashPathUnderHomeDirectory _packageExecutablePath;
        private readonly IEnvironmentProvider _environmentProvider;
        private readonly IReporter _reporter;


        public OsxZshEnvironmentPathInstruction(
            BashPathUnderHomeDirectory executablePath,
            IReporter reporter,
            IEnvironmentProvider environmentProvider
        )
        {
            _packageExecutablePath = executablePath;
            _environmentProvider
                = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
            _reporter
                = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        private bool PackageExecutablePathExists()
        {
            string value = _environmentProvider.GetEnvironmentVariable(PathName);
            if (value == null)
            {
                return false;
            }

            return value
                .Split(':')
                .Any(p => p == _packageExecutablePath.Path);
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExists())
            {
                // similar to https://code.visualstudio.com/docs/setup/mac
                _reporter.WriteLine(
                    string.Format(
                        CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                        _packageExecutablePath.Path));
            }
        }
    }
}
