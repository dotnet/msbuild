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

            if (existingUserEnvPath == null)
            {
                Environment.SetEnvironmentVariable(
                    PathName,
                    _packageExecutablePath,
                    EnvironmentVariableTarget.User);
            }
            else
            {
                if (existingUserEnvPath.EndsWith(';'))
                {
                    existingUserEnvPath = existingUserEnvPath.Substring(0, (existingUserEnvPath.Length - 1));
                }

                Environment.SetEnvironmentVariable(
                    PathName,
                    $"{existingUserEnvPath};{_packageExecutablePath}",
                    EnvironmentVariableTarget.User);

            }
        }

        private bool PackageExecutablePathExists()
        {
            return PackageExecutablePathExistsForCurrentProcess() || PackageExecutablePathWillExistForFutureNewProcess();
        }

        private bool PackageExecutablePathWillExistForFutureNewProcess()
        {
            return EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User))
                   || EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Machine));
        }

        private bool PackageExecutablePathExistsForCurrentProcess()
        {
            return EnvironmentVariableConatinsPackageExecutablePath(Environment.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Process));
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
            if (!PackageExecutablePathExistsForCurrentProcess() && PackageExecutablePathWillExistForFutureNewProcess())
            {
                _reporter.WriteLine(
                    CommonLocalizableStrings.EnvironmentPathWindowsNeedReopen);
            }
            else if (!PackageExecutablePathWillExistForFutureNewProcess())
            {
                _reporter.WriteLine(
                    string.Format(
                        CommonLocalizableStrings.EnvironmentPathWindowsManualInstructions,
                        _packageExecutablePath));
            }
        }
    }
}
