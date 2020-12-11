// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetNewCommand : TestCommand
    {
        public DotnetNewCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            // Set dotnet-new3.dll as first Argument to be passed to "dotnet"
            // And use full path since we want to execute in any working directory
            Arguments.Add(Path.GetFullPath("dotnet-new3.dll"));
            Arguments.AddRange(args);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = "dotnet",
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }

    public class DotnetCommand : TestCommand
    {
        private readonly string commandName;

        public DotnetCommand(ITestOutputHelper log, string commandName, params string[] args) : base(log)
        {
            Arguments.Add(commandName);
            Arguments.AddRange(args);
            this.commandName = commandName;
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = "dotnet",
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }
}
