using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace dotnet_new3
{
    public static class AssemblyLoadContextHelper
    {
        private static string _appDir;

        public static string AppDir
        {
            get
            {
                if (_appDir == null)
                {
                    Assembly asm = Assembly.GetEntryAssembly();
                    Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
                    string localPath = codebase.LocalPath;
                    _appDir = Path.GetDirectoryName(localPath);
                }

                return _appDir;
            }
        }

        public static IEnumerable<Assembly> LoadAllFromCodebase(this AssemblyLoadContext context, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(context, AppDir, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(this AssemblyLoadContext context, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            foreach (string file in Directory.EnumerateFiles(path, pattern, searchOption))
            {
                Assembly assembly = null;

                try
                {
                    assembly = context.LoadFromAssemblyPath(file);
                }
                catch
                {
                }

                if (assembly != null)
                {
                    yield return assembly;
                }
            }
        }
    }
}
