// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if !NET6_0_OR_GREATER
using System.Diagnostics;
#endif
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Build.UnitTests.Shared
{
    public class EnvironmentProvider
    {
        private static class Constants
        {
            public const string DotNet = "dotnet";
            public const string Path = "PATH";
            public const string DotnetMsbuildSdkResolverCliDir = "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR";
            public static readonly bool RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            public static readonly string ExeSuffix = RunningOnWindows ? ".exe" : string.Empty;
        }

        private IEnumerable<string>? _searchPaths;

        private readonly Func<string, string?> _getEnvironmentVariable;
        private readonly Func<string?> _getCurrentProcessPath;

        public EnvironmentProvider(Func<string, string?> getEnvironmentVariable)
            : this(getEnvironmentVariable, GetCurrentProcessPath)
        { }

        public EnvironmentProvider(Func<string, string?> getEnvironmentVariable, Func<string?> getCurrentProcessPath)
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
                        (_getEnvironmentVariable(Constants.Path) ?? string.Empty)
                        .Split(new char[] { Path.PathSeparator }, options: StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim('"')));

                    _searchPaths = searchPaths;
                }

                return _searchPaths;
            }
        }

        public string? GetCommandPath(string commandName)
        {
            var commandNameWithExtension = commandName + Constants.ExeSuffix;
            var commandPath = SearchPaths
                .Where(p => !Path.GetInvalidPathChars().Any(p.Contains))
                .Select(p => Path.Combine(p, commandNameWithExtension))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public string? GetDotnetExePath()
        {
            string? environmentOverride = _getEnvironmentVariable(Constants.DotnetMsbuildSdkResolverCliDir);
            if (!string.IsNullOrEmpty(environmentOverride))
            {
                return Path.Combine(environmentOverride, Constants.DotNet + Constants.ExeSuffix);
            }

            string? dotnetExe = _getCurrentProcessPath();

            if (string.IsNullOrEmpty(dotnetExe) || !Path.GetFileNameWithoutExtension(dotnetExe)
                    .Equals(Constants.DotNet, StringComparison.InvariantCultureIgnoreCase))
            {
                string? dotnetExeFromPath = GetCommandPath(Constants.DotNet);
#if NET
                if (dotnetExeFromPath != null && !Constants.RunningOnWindows)
                {
                    // on Linux the 'dotnet' command from PATH is a symlink so we need to
                    // resolve it to get the actual path to the binary
                    FileInfo fi = new FileInfo(dotnetExeFromPath);
                    while (fi.LinkTarget != null)
                    {
                        dotnetExeFromPath = fi.LinkTarget;
                        fi = new FileInfo(dotnetExeFromPath);
                    }
                }
#endif
                if (!string.IsNullOrWhiteSpace(dotnetExeFromPath))
                {
                    dotnetExe = dotnetExeFromPath;
                }
            }

            return dotnetExe;
        }

        public static string? GetDotnetExePath(Func<string, string?>? getEnvironmentVariable = null)
        {
            if (getEnvironmentVariable == null)
            {
                getEnvironmentVariable = Environment.GetEnvironmentVariable;
            }
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable);
            return environmentProvider.GetDotnetExePath();
        }

        public static string? GetDotnetExePath(Func<string, string?> getEnvironmentVariable, Func<string?> getCurrentProcessPath)
        {
            getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
            getCurrentProcessPath ??= GetCurrentProcessPath;
            var environmentProvider = new EnvironmentProvider(getEnvironmentVariable, getCurrentProcessPath);
            return environmentProvider.GetDotnetExePath();
        }

        private static string? GetCurrentProcessPath()
        {
            string? currentProcessPath;
#if NET6_0_OR_GREATER
            currentProcessPath = Environment.ProcessPath;
#else
            currentProcessPath = Process.GetCurrentProcess().MainModule.FileName;
#endif
            return currentProcessPath;
        }
    }
}
