// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetTestCommand : DotnetCommand
    {
        public DotnetTestCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("test");
            Arguments.AddRange(args);
        }
    }
}
