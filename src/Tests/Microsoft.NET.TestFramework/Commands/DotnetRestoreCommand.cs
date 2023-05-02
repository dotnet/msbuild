// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetRestoreCommand : DotnetCommand
    {
        public DotnetRestoreCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("restore");
            Arguments.AddRange(args);
        }
    }
}
