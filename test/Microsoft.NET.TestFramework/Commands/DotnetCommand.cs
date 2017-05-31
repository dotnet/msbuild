// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetCommand : TestCommand
    {
        public string WorkingDirectory { get; set; }

        public DotnetCommand(ITestOutputHelper log) : base(log)
        {
        }

        protected override ICommand CreateCommand(string[] args)
        {
            ICommand ret = Command.Create(RepoInfo.DotNetHostPath, args);
            if (!string.IsNullOrEmpty(WorkingDirectory))
            {
                ret = ret.WorkingDirectory(WorkingDirectory);
            }

            return ret;
        }
    }
}
