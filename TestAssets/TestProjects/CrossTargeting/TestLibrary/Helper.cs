// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

#if DESKTOP
using System.Windows.Forms;
#endif

namespace TestLibrary
{
    public static class Helper
    {
        /// <summary>
        /// Gets the message from the helper. This comment is here to help test XML documentation file generation, please do not remove it.
        /// </summary>
        /// <returns>A message</returns>
        public static string GetMessage()
        {
            return "This string came from the test library!";
        }

        public static void SayHi()
        {
#if DESKTOP
            MessageBox.Show("Hello there!");
#else            
            Console.WriteLine("Hello there!");
#endif        
        }
    }
}
