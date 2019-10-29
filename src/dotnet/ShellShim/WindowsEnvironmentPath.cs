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
        private readonly IEnvironmentProvider _environmentProvider;

        public WindowsEnvironmentPath(string packageExecutablePath, IReporter reporter, IEnvironmentProvider environmentProvider)
        {
            _packageExecutablePath = packageExecutablePath ?? throw new ArgumentNullException(nameof(packageExecutablePath));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
        }

        public void AddPackageExecutablePathToUserPath()
        {
            if (PackageExecutablePathExists())
            {
                return;
            }

            var existingUserEnvPath = _environmentProvider.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User);

            try
            {
                if (existingUserEnvPath == null)
                {
                    _environmentProvider.SetEnvironmentVariable(
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

                    _environmentProvider.SetEnvironmentVariable(
                        PathName,
                        $"{existingUserEnvPath};{_packageExecutablePath}",
                        EnvironmentVariableTarget.User);
                }
            }
            catch (System.Security.SecurityException)
            {
                _reporter.WriteLine(
                    string.Format(
                        CommonLocalizableStrings.FailedToSetToolsPathEnvironmentVariable,
                        _packageExecutablePath).Yellow());
            }
        }

        private bool PackageExecutablePathExists()
        {
            return PackageExecutablePathExistsForCurrentProcess() || PackageExecutablePathWillExistForFutureNewProcess();
        }

        private bool PackageExecutablePathWillExistForFutureNewProcess()
        {
            return EnvironmentVariableContainsPackageExecutablePath(_environmentProvider.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User))
                   || EnvironmentVariableContainsPackageExecutablePath(_environmentProvider.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Machine));
        }

        private bool PackageExecutablePathExistsForCurrentProcess()
        {
            return EnvironmentVariableContainsPackageExecutablePath(_environmentProvider.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Process));
        }

        private bool EnvironmentVariableContainsPackageExecutablePath(string environmentVariable)
        {
            if (environmentVariable == null)
            {
                return false;
            }

            return environmentVariable
                .Split(';')
                .Any(p => string.Equals(p, _packageExecutablePath, StringComparison.OrdinalIgnoreCase));
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExistsForCurrentProcess() && PackageExecutablePathWillExistForFutureNewProcess())
            {
                _reporter.WriteLine(CommonLocalizableStrings.EnvironmentPathWindowsNeedReopen);
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
