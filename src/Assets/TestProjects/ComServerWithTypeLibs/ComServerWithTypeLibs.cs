// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace COMServer
{
    // User defined attribute on types. Ensure that user defined
    // attributes don't break us from parsing metadata.
    public class UserDefinedAttribute : Attribute
    {
    }

    [UserDefined]
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
        [UserDefined]
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

    [ComVisible(true)]
    [Guid("e5381440-17ca-4807-803c-7e02fc14ce32")]
    [ProgId("")]
    public class ClassWithoutProgId
    { }

    [ComVisible(true)]
    [Guid("7d00a362-1dee-49d7-a6a0-9986ea02a676")]
    [ProgId("Explicit.ProgId")]
    public class ClassWithExplicitProgId
    { }
}
