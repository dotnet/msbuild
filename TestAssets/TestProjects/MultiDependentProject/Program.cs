// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using FluentAssertions;

class Program
{
    public static void Main(string[] args)
    {
        ArrayList argList = new ArrayList(args);
        JObject jObject = new JObject();

        foreach (string arg in argList)
        {
            jObject[arg] = arg;
        }

        jObject.Count.Should().Be(0);
        Console.WriteLine(jObject.ToString());
    }
}
