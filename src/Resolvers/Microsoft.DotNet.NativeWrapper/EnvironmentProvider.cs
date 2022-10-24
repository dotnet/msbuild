// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

//only Microsoft.DotNet.NativeWrapper (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

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

        public string GetDotnetExeDirectory(Action<FormattableString> log = null)
        {
            string environmentOverride = _getEnvironmentVariable(Constants.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR);
            if (!string.IsNullOrEmpty(environmentOverride))
            {
                log?.Invoke($"GetDotnetExeDirectory: {Constants.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR} set to {environmentOverride}");
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
                log?.Invoke($"GetDotnetExeDirectory: dotnet command path not found.  Using current process");
                log?.Invoke($"GetDotnetExeDirectory: Path variable: {_getEnvironmentVariable(Constants.PATH)}");

#if NET6_0_OR_GREATER
                dotnetExe = Environment.ProcessPath;
#else
                dotnetExe = Process.GetCurrentProcess().MainModule.FileName;
#endif
            }

            var dotnetDirectory = Path.GetDirectoryName(dotnetExe);

            log?.Invoke($"GetDotnetExeDirectory: Returning {dotnetDirectory}");

            return dotnetDirectory;
        }

        public static string GetDotnetExeDirectory(Func<string, string> getEnvironmentVariable = null, Action<FormattableString> log = null)
        {
            if (getEnvironmentVariable == null)
            {
                getEnvironmentVariable = Environment.GetEnvironmentVariable;
            }
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable);
            return environmentProvider.GetDotnetExeDirectory(log);
        }
    }
}
