// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Diagnostics;
using FluentAssertions;

namespace Microsoft.DotNet.Tests.ArgumentForwarding
{
    public class Program
    {
        public static void Main(string[] args)
        {
            bool first=true;
            foreach (var arg in args)
            {
                if (first)
                {
                    first=false;
                }
                else
                {
                    Console.Write(",");
                }
                Console.Write(arg);
            }
        }
    }
}