using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetBuildCommand : DotnetCommand
    {
        public DotnetBuildCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("build");
            Arguments.AddRange(args);
        }
    }
}
