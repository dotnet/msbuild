using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Env
    {
        private static IEnumerable<string> _searchPaths;
        private static IEnumerable<string> _executableExtensions;

        public static IEnumerable<string> ExecutableExtensions
        {
            get
            {
                if (_executableExtensions == null)
                {

                    _executableExtensions = PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows
                        ? Environment.GetEnvironmentVariable("PATHEXT")
                            .Split(';')
                            .Select(e => e.ToLower().Trim('"'))
                        : new [] { string.Empty };
                }

                return _executableExtensions;
            }
        } 

        private static IEnumerable<string> SearchPaths 
        {
            get
            {
                if (_searchPaths == null)
                {
                    var searchPaths = new List<string> { PlatformServices.Default.Application.ApplicationBasePath };

                    searchPaths.AddRange(Environment
                        .GetEnvironmentVariable("PATH")
                        .Split(Path.PathSeparator)
                        .Select(p => p.Trim('"')));

                    _searchPaths = searchPaths;
                }

                return _searchPaths;
            }
        }

        public static string GetCommandPath(string commandName, params string[] extensions)
        {
            if (!extensions.Any())
            {
                extensions = Env.ExecutableExtensions.ToArray();
            }

            var commandPath = Env.SearchPaths.Join(
                extensions,
                    p => true, s => true,
                    (p, s) => Path.Combine(p, commandName + s))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public static string GetCommandPathFromAppBase(string appBase, string commandName, params string[] extensions)
        {
            if (!extensions.Any())
            {
                extensions = Env.ExecutableExtensions.ToArray();
            }

            var commandPath = extensions.Select(e => Path.Combine(appBase, commandName + e))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }
    }
}
