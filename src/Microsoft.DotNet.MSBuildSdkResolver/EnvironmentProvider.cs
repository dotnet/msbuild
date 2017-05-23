// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal class EnvironmentProvider
    {
        private IEnumerable<string> _searchPaths;
        private IEnumerable<string> _executableExtensions;

        private readonly Func<string, string> _getEnvironmentVariable;

        public EnvironmentProvider(Func<string, string> getEnvironmentVariable)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
        }

        public IEnumerable<string> ExecutableExtensions
        {
            get
            {
                if (_executableExtensions == null)
                {

                    _executableExtensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? _getEnvironmentVariable("PATHEXT")
                            .Split(';')
                            .Select(e => e.ToLower().Trim('"'))
                        : new [] { string.Empty };
                }

                return _executableExtensions;
            }
        }

        private IEnumerable<string> SearchPaths
        {
            get
            {
                if (_searchPaths == null)
                {
                    var searchPaths = new List<string> { GetApplicationBasePath() };

                    searchPaths.AddRange(
                        _getEnvironmentVariable("PATH")
                        .Split(Path.PathSeparator)
                        .Select(p => p.Trim('"')));

                    _searchPaths = searchPaths;
                }

                return _searchPaths;
            }
        }

        public string GetCommandPath(string commandName)
        {
            var commandPath = SearchPaths.Join(
                ExecutableExtensions.ToArray(),
                    p => true, s => true,
                    (p, s) => Path.Combine(p, commandName + s))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        private static string GetApplicationBasePath()
        {
            return Path.GetFullPath(AppContext.BaseDirectory);
        }
    }
}
