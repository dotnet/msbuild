// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : CommandBase
    {
        public WorkloadUpdateCommand(
            ParseResult result)
            : base(result)
        {
        }

        public override int Execute()
        {
            // TODO stub
            Reporter.Output.WriteLine("WIP workload update");
            return 0;
        }
    }
}
