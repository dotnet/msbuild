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
