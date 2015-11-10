using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.DotNet.ProjectModel.Loader
{
    public class ProjectContextLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<AssemblyName, string> _assemblies;
        private readonly Dictionary<string, string> _dllImports;
        public ProjectContextLoadContext(Dictionary<AssemblyName, string> assemblies, 
                                         Dictionary<string, string> dllImports)
        {
            _assemblies = assemblies;
            _dllImports = dllImports;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string path;
            if (_assemblies.TryGetValue(assemblyName, out path))
            {
                return LoadFromAssemblyPath(path);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string path;
            if (_dllImports.TryGetValue(unmanagedDllName, out path))
            {
                return LoadUnmanagedDllFromPath(path);
            }

            return base.LoadUnmanagedDll(unmanagedDllName);
        }
     }
}