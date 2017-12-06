// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.NET.TestFramework
{
    public static class CommandExtensions
    {
        public static ICommand EnsureExecutable(this ICommand command)
        {
            //  Workaround for https://github.com/NuGet/Home/issues/4424
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Command.Create("chmod", new[] { "755", command.CommandName })
                    .Execute()
                    .Should()
                    .Pass();
            }
            return command;
        }
    }
}
