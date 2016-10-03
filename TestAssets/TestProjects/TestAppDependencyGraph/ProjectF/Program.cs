// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace TestApp
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("This string came from ProjectF");
            string helperStr = TestLibrary.ProjectG.GetMessage();
            Console.WriteLine(helperStr);
            return 0;
        }
    }
}
