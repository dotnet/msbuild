// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("TestApp --depends on--> MainLibrary --depends on--> AuxLibrary");
            Console.WriteLine(MainLibrary.Helper.GetMessage());
            Console.WriteLine(AuxLibrary.Helper.GetMessage());
        }
    }
}
