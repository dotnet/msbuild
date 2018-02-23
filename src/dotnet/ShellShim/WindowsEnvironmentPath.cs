// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal class WindowsEnvironmentPath : IEnvironmentPath
    {
        private readonly IReporter _reporter;
        private const string PathName = "PATH";
        private readonly string _packageExecutablePath;

        public WindowsEnvironmentPath(
            string packageExecutablePath, IReporter reporter)
        {
            _packageExecutablePath
                = packageExecutablePath ?? throw new ArgumentNullException(nameof(packageExecutablePath));
            _reporter
                = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void AddPackageExecutablePathToUserPath()
        {
            if (PackageExecutablePathExists())
            {
                return;
            }

            var existingUserEnvPath = Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User);

            Environment.SetEnvironmentVariable(
                PathName,
                $"{existingUserEnvPath};{_packageExecutablePath}",
                EnvironmentVariableTarget.User);
        }

        private bool PackageExecutablePathExists()
        {
            return EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User))
                || EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Machine))
                || EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Process));
        }

        private bool EnvironmentVariableConatinsPackageExecutablePath(string environmentVariable)
        {
            if (environmentVariable == null)
            {
                return false;
            }

            return environmentVariable.Split(';').Contains(_packageExecutablePath);
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExists())
            {
                if (Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User).Split(';')
                    .Contains(_packageExecutablePath))
                {
                    _reporter.WriteLine(
                        CommonLocalizableStrings.EnvironmentPathWindowsNeedReopen);
                }
                else
                {
                    _reporter.WriteLine(
                        string.Format(
                            CommonLocalizableStrings.EnvironmentPathWindowsManualInstructions,
                            _packageExecutablePath));
                }
            }
        }
    }
}
