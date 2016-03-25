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
        private static IEnvironmentProvider _environment = new EnvironmentProvider();

        public static IEnumerable<string> ExecutableExtensions
        {
            get
            {
                return _environment.ExecutableExtensions;
            }
        }

        public static string GetCommandPath(string commandName, params string[] extensions)
        {
            return _environment.GetCommandPath(commandName, extensions);
        }

        public static string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        {
            return _environment.GetCommandPathFromRootPath(rootPath, commandName, extensions);
        }

        public static string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
        {
            return _environment.GetCommandPathFromRootPath(rootPath, commandName, extensions);
        }

        public static bool GetBool(string name, bool defaultValue = false)
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
