using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetToolCommand : DotnetCommand
    {
        public DotnetToolCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("tool");
            Arguments.AddRange(args);
        }
    }
}
