using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
#if FEATURE_VISUALSTUDIOSETUP
using Microsoft.VisualStudio.Setup.Configuration;
#endif

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Helper class to wrap the Microsoft.VisualStudio.Setup.Configuration.Interop API to query
    /// Visual Studio setup for instances installed on the machine.
    /// Code derived from sample: https://code.msdn.microsoft.com/Visual-Studio-Setup-0cedd331
    /// </summary>
    internal class VisualStudioLocationHelper
    {
#if FEATURE_VISUALSTUDIOSETUP
        private const int REGDB_E_CLASSNOTREG = unchecked((int) 0x80040154);
#endif // FEATURE_VISUALSTUDIOSETUP

        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        internal static IList<VisualStudioInstance> GetInstances()
        {
            var validInstances = new List<VisualStudioInstance>();

#if FEATURE_VISUALSTUDIOSETUP
            try
            {
                // This code is not obvious. See the sample (link above) for reference.
                var query = (ISetupConfiguration2) GetQuery();
                var e = query.EnumAllInstances();

                int fetched;
                var instances = new ISetupInstance[1];
                do
                {
                    // Call e.Next to query for the next instance (single item or nothing returned).
                    e.Next(1, instances, out fetched);
                    if (fetched <= 0) continue;

                    var instance = instances[0];
                    var state = ((ISetupInstance2) instance).GetState();
                    Version version;

                    try
                    {
                        version = new Version(instance.GetInstallationVersion());
                    }
                    catch (FormatException)
                    {
                        continue;
                    }

                    // If the install was complete and a valid version, consider it.
                    if (state == InstanceState.Complete)
                    {
                        validInstances.Add(new VisualStudioInstance(
                            instance.GetDisplayName(),
                            instance.GetInstallationPath(),
                            version));
                    }
                } while (fetched > 0);
            }
            catch (COMException)
            { }
            catch (DllNotFoundException)
            { 
                // This is OK, VS "15" or greater likely not installed.
            }
#endif
            return validInstances;
        }

#if FEATURE_VISUALSTUDIOSETUP
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

        [DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int GetSetupConfiguration(
            [MarshalAs(UnmanagedType.Interface), Out] out ISetupConfiguration configuration,
            IntPtr reserved);
#endif
    }

    /// <summary>
    /// Wrapper class to represent an installed instance of Visual Studio.
    /// </summary>
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
}
