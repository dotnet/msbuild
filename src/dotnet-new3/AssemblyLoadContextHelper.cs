using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace dotnet_new3
{
    public static class AssemblyLoadContextHelper
    {
        public static IEnumerable<Assembly> LoadAllFromCodebase(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(context, out loadFailures, Paths.AppDir, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(this AssemblyLoadContext context, out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            List<Assembly> loaded = new List<Assembly>();
            List<string> failures = new List<string>();

            foreach (string file in Directory.EnumerateFiles(path, pattern, searchOption))
            {
                try
                {
                    Assembly assembly = context.LoadFromAssemblyPath(file);
                    loaded.Add(assembly);
                }
                catch
                {
                    failures.Add(file);
                }
            }

            loadFailures = failures;
            return loaded;
        }
    }
}
