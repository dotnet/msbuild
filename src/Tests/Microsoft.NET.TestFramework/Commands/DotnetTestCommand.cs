using System;
using System.Collections.Generic;
using System.Text;
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
