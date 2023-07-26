// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public class DotnetNewCommand : DotnetCommand
    {
        private bool _hiveSet;

        public DotnetNewCommand(ITestOutputHelper log, params string[] args) : base(log)
        {
            Arguments.Add("new");
            Arguments.AddRange(args);

            //opt out from telemetry
            WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
        }

        public DotnetNewCommand WithVirtualHive()
        {
            Arguments.Add("--debug:ephemeral-hive");
            _hiveSet = true;
            return this;
        }


        public DotnetNewCommand WithCustomHive(string path)
        {
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

        public DotnetNewCommand WithoutBuiltInTemplates()
        {
            Arguments.Add("--debug:disable-sdk-templates");
            return this;
        }

        public DotnetNewCommand WithDebug()
        {
            WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true");
            return this;
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            if (!_hiveSet)
            {
                throw new Exception($"\"--debug:custom-hive\" is not set, call {nameof(WithCustomHive)} to set it or {nameof(WithoutCustomHive)} if it is intentional.");
            }

            return base.CreateCommand(args);
        }
    }
}
