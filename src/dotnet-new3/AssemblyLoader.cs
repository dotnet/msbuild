using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace dotnet_new3
{
    public static class AssemblyLoader
    {
        public static void Load(string path)
        {
            AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        public static IEnumerable<Assembly> LoadAllAssemblies(out IEnumerable<string> loadFailures, string componentsDir, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            IEnumerable<string>
                failures1,
                failures2;

            IEnumerable<Assembly> loaded = LoadAllFromUserDir(out failures1, componentsDir, pattern, searchOption).Union(LoadAllFromCodebase(out failures2, pattern, searchOption));
            loadFailures = failures1.Union(failures2);
            return loaded;
        }

        public static IEnumerable<Assembly> LoadAllFromCodebase(out IEnumerable<string> loadFailures, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return AssemblyLoadContext.Default.LoadAllFromCodebase(out loadFailures, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromUserDir(out IEnumerable<string> loadFailures, string componentsDir, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return LoadAllFromPath(out loadFailures, componentsDir, pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(out IEnumerable<string> loadFailures, string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return AssemblyLoadContext.Default.LoadAllFromPath(out loadFailures, path, pattern, searchOption);
        }
    }
}
