using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
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
        bool _hiveSet = false;

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

            if (!_hiveSet)
            {
                throw new Exception($"\"--debug:custom-hive\" is not set, call {nameof(WithCustomHive)} to set it or {nameof(WithoutCustomHive)} if it is intentional.");
            }

            return sdkCommandSpec;
        }

        public DotnetNewCommand WithCustomHive(string path = null)
        {
            path ??= TestUtils.CreateTemporaryFolder();
            Arguments.Add("--debug:custom-hive");
            Arguments.Add(path);
            _hiveSet = true;
            return this;
        }

        public DotnetNewCommand WithoutCustomHive()
        {
            _hiveSet = true;
            return this;
        }
    }
}
