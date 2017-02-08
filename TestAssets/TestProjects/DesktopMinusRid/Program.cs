// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TestApp
{
    class Program
    {
        static void Main()
        {
            // Prevent libuv on path from interfering with test
            Environment.SetEnvironmentVariable("PATH", "");

#if USE_NATIVE_CODE
            try
            {
                uv_loop_size();
                Console.WriteLine($"Native code was used ({GetProcessorArchitecture()})");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine($"Native code failed ({GetProcessorArchitecture()})");
            }
#else      
            Console.WriteLine($"Native code was not used ({GetProcessorArchitecture()})");
#endif  
        }

#if USE_NATIVE_CODE
        [DllImport("libuv", CallingConvention = CallingConvention.Cdecl)]
        static extern int uv_loop_size();
#endif

        static ProcessorArchitecture GetProcessorArchitecture()
        {
            return AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).ProcessorArchitecture;
        }
    }
}
