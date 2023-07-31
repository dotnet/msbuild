// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public class RunExeCommand : TestCommand
    {
        private readonly string _commandPath;

        public RunExeCommand(ITestOutputHelper log, string commandPath, params string[] args) : base(log)
        {
            if (!File.Exists(commandPath) && !ToolsetInfo.TryResolveCommand(commandPath, out _))
            {
                throw new ArgumentException($"Cannot find command {commandPath}");
            }

            _commandPath = commandPath;
            Arguments.AddRange(args);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = _commandPath,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory,
            };
            TestContext.Current.AddTestEnvironmentVariables(sdkCommandSpec.Environment);
            return sdkCommandSpec;
        }
    }
}
