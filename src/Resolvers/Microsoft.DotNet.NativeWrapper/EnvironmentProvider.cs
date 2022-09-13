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
        private readonly Func<string> _getCurrentProcessPath;

        public EnvironmentProvider(Func<string, string> getEnvironmentVariable)
            : this(getEnvironmentVariable, GetCurrentProcessPath)
        { }

        public EnvironmentProvider(Func<string, string> getEnvironmentVariable, Func<string> getCurrentProcessPath)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _getCurrentProcessPath = getCurrentProcessPath;
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

            string dotnetExe = _getCurrentProcessPath();

            if (string.IsNullOrEmpty(dotnetExe) || !Path.GetFileNameWithoutExtension(dotnetExe)
                    .Equals(Constants.DotNet, StringComparison.InvariantCultureIgnoreCase))
            {
                string dotnetExeFromPath = GetCommandPath(Constants.DotNet);
                
                if (dotnetExeFromPath != null && !Interop.RunningOnWindows)
                {
                    // e.g. on Linux the 'dotnet' command from PATH is a symlink so we need to
                    // resolve it to get the actual path to the binary
                    dotnetExeFromPath = Interop.Unix.realpath(dotnetExeFromPath) ?? dotnetExeFromPath;
                }

                if (!string.IsNullOrWhiteSpace(dotnetExeFromPath))
                {
                    dotnetExe = dotnetExeFromPath;
                }
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

        public static string GetDotnetExeDirectory(Func<string, string> getEnvironmentVariable, Func<string> getCurrentProcessPath)
        {
            getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
            getCurrentProcessPath ??= GetCurrentProcessPath;
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable, getCurrentProcessPath);
            return environmentProvider.GetDotnetExeDirectory();
        }

        private static string GetCurrentProcessPath()
        {
            string currentProcessPath;
#if NET6_0_OR_GREATER
            currentProcessPath = Environment.ProcessPath;
#else
            currentProcessPath = Process.GetCurrentProcess().MainModule.FileName;
#endif
            return currentProcessPath;
        }
    }
}
