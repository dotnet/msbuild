// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using NuGet.Configuration;

class Program
{
    public static void Main(string[] args)
    {
        var settingValue = new SettingValue("key", "value", false);

        Console.WriteLine(settingValue.Key);
    }
}
