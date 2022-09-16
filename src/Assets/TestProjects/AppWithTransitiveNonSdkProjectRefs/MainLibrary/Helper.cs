// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MainLibrary
{
    public static class Helper
    {
        public static void WriteMessage()
        {
            Console.WriteLine("This string came from MainLibrary!");
            AuxLibrary.Helper.WriteMessage();
        }
    }
}
