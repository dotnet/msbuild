// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShimMaker
{
    internal class OSXEnvironmentPath : IEnvironmentPath
    {
        private const string PathName = "PATH";
        private readonly string _packageExecutablePathWithTilde;
        private readonly string _fullPackageExecutablePath;
        private readonly IFile _fileSystem;
        private readonly IEnvironmentProvider _environmentProvider;
        private readonly IReporter _reporter;

        private static readonly string PathDDotnetCliToolsPath
            = Environment.GetEnvironmentVariable("DOTNET_CLI_TEST_OSX_PATHSD_PATH")
              ?? @"/etc/paths.d/dotnet-cli-tools";

        public OSXEnvironmentPath(
            string packageExecutablePathWithTilde,
            string fullPackageExecutablePath,
            IReporter reporter,
            IEnvironmentProvider environmentProvider,
            IFile fileSystem
        )
        {
            _fullPackageExecutablePath = fullPackageExecutablePath ??
                                         throw new ArgumentNullException(nameof(fullPackageExecutablePath));
            _packageExecutablePathWithTilde = packageExecutablePathWithTilde ??
                                              throw new ArgumentNullException(nameof(packageExecutablePathWithTilde));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _environmentProvider
                = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
            _reporter
                = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void AddPackageExecutablePathToUserPath()
        {
            if (PackageExecutablePathExists())
            {
                return;
            }

            var script = $"{_packageExecutablePathWithTilde}";
            _fileSystem.WriteAllText(PathDDotnetCliToolsPath, script);
        }

        private bool PackageExecutablePathExists()
        {
            return _environmentProvider.GetEnvironmentVariable(PathName).Split(':')
                       .Contains(_packageExecutablePathWithTilde) ||
                   _environmentProvider.GetEnvironmentVariable(PathName).Split(':')
                       .Contains(_fullPackageExecutablePath);
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
            if (!PackageExecutablePathExists())
            {
                if (_fileSystem.Exists(PathDDotnetCliToolsPath))
                {
                    _reporter.WriteLine(
                        "Since you just installed the .NET Core SDK, you will need to reopen terminal before running the tool you installed.");
                }
                else
                {
                    // similar to https://code.visualstudio.com/docs/setup/mac
                    _reporter.WriteLine(
                        $"Cannot find the tools executable path. Please ensure {_fullPackageExecutablePath} is added to your PATH.{Environment.NewLine}" +
                        $"If you are using bash, You can do this by running the following command:{Environment.NewLine}{Environment.NewLine}" +
                        $"cat << EOF >> ~/.bash_profile{Environment.NewLine}" +
                        $"# Add .NET Core SDK tools{Environment.NewLine}" +
                        $"export PATH=\"$PATH:{_fullPackageExecutablePath}\"{Environment.NewLine}" +
                        $"EOF");
                }
            }
        }
    }
}
