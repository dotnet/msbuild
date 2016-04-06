using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class EnvironmentProvider : IEnvironmentProvider
    {
        private IEnumerable<string> _searchPaths;
        private IEnumerable<string> _executableExtensions;

        public IEnumerable<string> ExecutableExtensions
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

        private IEnumerable<string> SearchPaths 
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

        public EnvironmentProvider(
            IEnumerable<string> extensionsOverride = null,
            IEnumerable<string> searchPathsOverride = null)
        {
            _executableExtensions = extensionsOverride;
            _searchPaths = searchPathsOverride;
        }

        public string GetCommandPath(string commandName, params string[] extensions)
        {
            if (!extensions.Any())
            {
                extensions = ExecutableExtensions.ToArray();
            }

            var commandPath = SearchPaths.Join(
                extensions,
                    p => true, s => true,
                    (p, s) => Path.Combine(p, commandName + s))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        {
            if (!extensions.Any())
            {
                extensions = ExecutableExtensions.ToArray();
            }

            var commandPath = extensions.Select(e => Path.Combine(rootPath, commandName + e))
                .FirstOrDefault(File.Exists);

            return commandPath;
        }

        public string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
        {
            var extensionsArr = extensions.OrEmptyIfNull().ToArray();

            return GetCommandPathFromRootPath(rootPath, commandName, extensionsArr);
        }

        public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
        {
            var str = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            switch (str.ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                    return true;
                case "false":
                case "0":
                case "no":
                    return false;
                default:
                    return defaultValue;
            }
        }

    }
}
