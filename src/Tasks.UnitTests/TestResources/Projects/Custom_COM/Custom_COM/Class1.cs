// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Custom_COM
{
    [Guid("D6F88E95-8A27-4ae6-B6DE-0542A0FC7039")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ITest
    {
    }

    [Guid("13FE32AD-4BF8-495f-AB4D-6C61BD463EA4")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("Tester.Numbers")]
    public class Test : ITest
    {
    }
}
