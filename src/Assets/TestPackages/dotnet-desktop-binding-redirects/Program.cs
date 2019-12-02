// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace BindingRedirects
{
    public class Program
    {
        private const string ExpectedNewtonSoftVersion = "8.0.0.0";

        public static int Main(string[] args)
        {
            return VerifyJsonLoad();
        }

        private static int VerifyJsonLoad()
        {
            WriteLine("=======Verifying Redirected Newtonsoft.Json assembly load=======");

            int result = 0;

            try
            {
                var jsonAsm = typeof(Newtonsoft.Json.JsonConvert).Assembly;
                var version = jsonAsm.GetName().Version.ToString();
                if (version != ExpectedNewtonSoftVersion)
                {
                    WriteLine($"Failure - Newtonsoft.Json: ExpectedVersion - {ExpectedNewtonSoftVersion}, ActualVersion - {version}");
                    result = -1;
                }
            }
            catch (Exception ex)
            {
                WriteLine($"Failed to load type 'Newtonsoft.Json.JsonConvert'");
                throw ex;
            }

            return result;
        }

        private static void WriteLine(string str)
        {
            var currentAssembly = Assembly.GetExecutingAssembly().GetName().Name;
            Console.WriteLine($"{currentAssembly}: {str}");
        }
    }
}
