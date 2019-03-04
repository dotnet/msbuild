// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace COMServer
{
    [ComVisible(true)]
    [Guid("17B6329E-B025-4FC7-A854-97D34600C5A6")]
    public class Class1
    {
    }

    class Class2
    {
        [ComVisible(true)]
        [Guid("D88137C9-9B6F-4B46-AA3D-55791BF906EE")]
        public class Class3
        { }

    }

    [ComVisible(true)]
    [Guid("D88137C9-9B6F-4B46-AA3D-55791BF906EE")]
    public class Class4
    {
        [ComVisible(true)]
        [Guid("D88137C9-9B6F-4B46-AA3D-55791BF906ED")]
        public class Class5
        { }

    }

    [ComVisible(true)]
    [Guid("808C614F-9571-4FEC-81F8-21B6FF7B0FED")]
    public struct S
    {

    }
}
