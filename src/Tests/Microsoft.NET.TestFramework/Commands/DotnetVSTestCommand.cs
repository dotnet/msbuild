using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetVSTestCommand : DotnetCommand
    {
        public DotnetVSTestCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("vstest");
            Arguments.AddRange(args);
        }
    }
}
