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
