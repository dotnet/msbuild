using System;
using System.Collections.Generic;
using System.IO;
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

        public DotnetBuildCommand(TestAsset testAsset, params string[] args) : this(testAsset.Log, args)
        {
            WorkingDirectory = Path.Combine(testAsset.TestRoot, testAsset.TestProject.Name);
        }
    }
}
