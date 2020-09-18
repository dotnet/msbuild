// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.UnGAC
{
    /// <summary>
    /// Original Issue: https://github.com/dotnet/msbuild/issues/5183
    /// This tool was created to help prevent customers from putting MSBuild assemblies in the Global Assembly Cache.
    /// It runs at VS install-time as well as repair-time.
    /// It is intended to run as best effort. Meaning that if it fails, we avoid throwing and instead log it.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string[] assembliesToUnGAC =
                {
                    "Microsoft.Build, Version=15.1.0.0",
                    "Microsoft.Build.Engine, Version=15.1.0.0",
                    "Microsoft.Build.Framework, Version=15.1.0.0",
                    "Microsoft.Build.Tasks.Core, Version=15.1.0.0",
                    "Microsoft.Build.Utilities.Core, Version=15.1.0.0",
                    "Microsoft.Build.Conversion.Core, Version=15.1.0.0"
                };

                uint hresult = NativeMethods.CreateAssemblyCache(out IAssemblyCache assemblyCache, 0);

                // Most significant bit is set, meaning there was an error in the Hresult.
                if ((hresult >> 31) == 1)
                {
                    Console.WriteLine($"Could not successfully call CreateAssemblyCache. HResult: {hresult}");
                    Console.WriteLine("Exiting without removing assemblies from the GAC...");
                    return;
                }

                foreach (string assembly in assembliesToUnGAC)
                {
                    hresult = assemblyCache.UninstallAssembly(dwFlags: 0, pszAssemblyName: assembly, pRefData: IntPtr.Zero, pulDisposition: 0);

                    Console.WriteLine($"Tried to remove {assembly} from the GAC. HResult: 0x{hresult:X8}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Caught an exception! We don't want to throw because we want MSBuild to install.\n" + e.ToString());
            }
        }
    }
}
