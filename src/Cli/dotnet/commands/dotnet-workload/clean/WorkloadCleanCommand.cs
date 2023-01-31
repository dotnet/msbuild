// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Workloads.Workload.Clean
{
    internal class WorkloadCleanCommand : WorkloadCommandBase
    {
        private readonly bool _cleanAll;

        public WorkloadCleanCommand(ParseResult parseResult) : base(parseResult)
        {
            _cleanAll = parseResult.GetValue(WorkloadCleanCommandParser.CleanAllOption);
        }

        public override int Execute()
        {
            return 0;
        }

    }
}
