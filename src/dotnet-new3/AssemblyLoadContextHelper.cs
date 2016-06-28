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
            // Console.WriteLine($"PATH: {path}");

            List<Assembly> loaded = new List<Assembly>();
            List<string> failures = new List<string>();

            foreach (string file in path.EnumerateFiles(pattern, searchOption))
            {
                try
                {
                    // Console.WriteLine($"Checking: {file}");
                    Assembly assembly = context.LoadFromAssemblyPath(file);
                    loaded.Add(assembly);
                }
                // catch (FileLoadException fex)
                // {
                //     Console.WriteLine($"FileName: {fex.FileName}");
                //     Exception iex = fex;
                //     while(iex != null)
                //     {
                //         Console.WriteLine(iex);
                //         iex = iex.InnerException;
                //     }
                // }
                catch //(Exception ex)
                {
                    // while(ex != null)
                    // {
                    //     Console.WriteLine(ex);
                    //     ex = ex.InnerException;
                    // }

                    failures.Add(file);
                }

                // Console.WriteLine(" ");
                // Console.WriteLine(" ");
            }

            loadFailures = failures;
            return loaded;
        }
    }
}
