using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Setup.Configuration;

namespace Microsoft.Build.Shared
{
    internal class VisualStudioInstance
    {
        /// <summary>
        /// Version of the Visual Studio Instance
        /// </summary>
        internal Version Version { get; }

        /// <summary>
        /// Path to the Visual Studio installation
        /// </summary>
        internal string Path { get; }

        /// <summary>
        /// Full name of the Visual Studio instance with SKU name
        /// </summary>
        internal string Name { get; }

        internal VisualStudioInstance(string name, string path, Version version)
        {
            Name = name;
            Path = path;
            Version = version;
        }
    }

    internal class VisualStudioLocationHelper
    {
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        internal static IEnumerable<VisualStudioInstance> GetInstances()
        {
            var validInstances = new List<VisualStudioInstance>();

            try
            {
                var query = (ISetupConfiguration2)GetQuery();
                var helper = (ISetupHelper)query;

                var e = query.EnumAllInstances();

                int fetched;
                var instances = new ISetupInstance[1];
                do
                {
                    e.Next(1, instances, out fetched);
                    if (fetched > 0)
                    {
                        var instance = PrintInstance(instances[0], helper);
                        if (instance != null)
                        {
                            validInstances.Add(instance);
                        }
                    }
                }
                while (fetched > 0);

                return validInstances;
            }
            catch (Exception)
            {
                return validInstances;
            }
        }

        private static ISetupConfiguration GetQuery()
        {
            try
            {
                // Try to CoCreate the class object.
                return new SetupConfiguration();
            }

            catch (COMException ex) when (ex.ErrorCode == REGDB_E_CLASSNOTREG)
            {
                // Try to get the class object using app-local call.
                ISetupConfiguration query;
                var result = GetSetupConfiguration(out query, IntPtr.Zero);

                if (result < 0)
                {
                    throw new COMException("Failed to get query", result);
                }

                return query;
            }
        }

        private static VisualStudioInstance PrintInstance(ISetupInstance instance, ISetupHelper helper)
        {
            var instance2 = (ISetupInstance2)instance;
            
            var state = instance2.GetState();
            if (state != InstanceState.Complete)
            {
                return null;
            }

            return new VisualStudioInstance(
                instance.GetDisplayName(),
                instance.GetInstallationPath(),
                new Version(instance.GetInstallationVersion()));
        }

        [DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int GetSetupConfiguration(
            [MarshalAs(UnmanagedType.Interface), Out] out ISetupConfiguration configuration,
            IntPtr reserved);
    }
}
