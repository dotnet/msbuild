// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace TestLibrary
{
    public static class Helper
    {
        /// <summary>
        /// Gets the message from the helper. This comment is here to help test XML documentation file generation, do not remove it.
        /// </summary>
        /// <returns>A message</returns>
        public static string GetMessage()
        {
            return "This string came from the test library!";
        }

        public static void SayHi()
        {
            Console.WriteLine("Hello there!");
        }
    }
}