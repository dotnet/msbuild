// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.UnGAC
{
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

                for (int i = 0; i < assembliesToUnGAC.Length; i++)
                {
                    hresult = assemblyCache.UninstallAssembly(dwFlags: 0, pszAssemblyName: assembliesToUnGAC[i], refData: IntPtr.Zero, pulDisposition: 0);

                    // If we hit an error with an assembly, keep trying the others.
                    if ((hresult >> 31) == 1)
                    {
                        Console.WriteLine($"Could not remove {assembliesToUnGAC[i]} from the GAC. HResult: {hresult}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Caught an exception! We don't want to throw because we want MSBuild to install.\n" + e.ToString());
            }
        }
    }
}
