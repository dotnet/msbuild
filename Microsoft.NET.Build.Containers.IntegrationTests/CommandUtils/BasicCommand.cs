// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace Microsoft.DotNet.CommandUtils
{
    internal sealed class BasicCommand : TestCommand
    {
        private readonly string _processName;

        internal BasicCommand(ITestOutputHelper? log, string processName, params string[] args) : base(log)
        {
            _processName = processName;
            Arguments.AddRange(args.Where(a => !string.IsNullOrWhiteSpace(a)));
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = _processName,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }
}
