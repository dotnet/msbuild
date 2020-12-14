using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
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
            if (!_environment.ContainsKey(Helpers.HomeEnvironmentVariableName))
            {
                throw new Exception($"{nameof(Helpers.HomeEnvironmentVariableName)} is not set, call {nameof(DotnetNewCommand)}{nameof(WithEnvironmentVariable)} to set it.");
            }
            return sdkCommandSpec;
        }
    }

}
