using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace dotnet_new3
{
    public static class AssemblyLoader
    {
        public static IEnumerable<Assembly> LoadAllFromCodebase(string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return AssemblyLoadContext.Default.LoadAllFromCodebase(pattern, searchOption);
        }

        public static IEnumerable<Assembly> LoadAllFromPath(string path, string pattern = "*.dll", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return AssemblyLoadContext.Default.LoadAllFromPath(path, pattern, searchOption);
        }
    }
}
