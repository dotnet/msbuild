// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

#nullable disable

namespace Microsoft.DotNet.NativeWrapper
{
    public class EnvironmentProvider
    {
        private IEnumerable<string> _searchPaths;

        private readonly Func<string, string> _getEnvironmentVariable;

        public EnvironmentProvider(Func<string, string> getEnvironmentVariable)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
        }

        private IEnumerable<string> SearchPaths
        {
            get
            {
                if (_searchPaths == null)
                {
                    var searchPaths = new List<string>();

                    searchPaths.AddRange(
                        _getEnvironmentVariable(Constants.PATH)
                        .Split(new char[] { Path.PathSeparator }, options: StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim('"')));

                    _searchPaths = searchPaths;
                }

                return _searchPaths;
            }
        }

        public string GetCommandPath(string commandName)
        {
            var commandNameWithExtension = commandName + Constants.ExeSuffix;
            var commandPath = SearchPaths
                .Where(p => !Path.GetInvalidPathChars().Any(c => p.Contains(c)))
                .Select(p => Path.Combine(p, commandNameWithExtension))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public string GetDotnetExeDirectory()
        {
            string environmentOverride = _getEnvironmentVariable(Constants.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR);
            if (!string.IsNullOrEmpty(environmentOverride))
            {
                return environmentOverride;
            }

            var dotnetExe = GetCommandPath(Constants.DotNet);

            if (dotnetExe != null && !Interop.RunningOnWindows)
            {
                // e.g. on Linux the 'dotnet' command from PATH is a symlink so we need to
                // resolve it to get the actual path to the binary
                dotnetExe = Interop.Unix.realpath(dotnetExe) ?? dotnetExe;
            }

            if (string.IsNullOrWhiteSpace(dotnetExe))
            {
                dotnetExe = Process.GetCurrentProcess().MainModule.FileName;
            }

            return Path.GetDirectoryName(dotnetExe);
        }

        public static string GetDotnetExeDirectory(Func<string, string> getEnvironmentVariable = null)
        {
            if (getEnvironmentVariable == null)
            {
                getEnvironmentVariable = Environment.GetEnvironmentVariable;
            }
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable);
            return environmentProvider.GetDotnetExeDirectory();
        }
    }
}
