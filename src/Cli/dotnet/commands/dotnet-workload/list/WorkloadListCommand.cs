// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadListCommand : CommandBase
    {
        private readonly bool _machineReadableOption;

        private readonly string _mockInstallDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath,
            "DEV_mockworkloads");

        public WorkloadListCommand(
            ParseResult result
        )
            : base(result)
        {
            _machineReadableOption = result.ValueForOption<bool>(WorkloadListCommandParser.MachineReadableOption);
        }

        public override int Execute()
        {
            // TODO stub
            var installedList = new List<string>();
            if (_machineReadableOption)
            {
                if (File.Exists(Path.Combine(_mockInstallDirectory, "Microsoft.iOS.Bundle.6.0.100.nupkg")))
                {
                    installedList.Add("mobile-ios");
                }

                if (File.Exists(Path.Combine(_mockInstallDirectory, "Microsoft.NET.Workload.Android.6.0.100.nupkg")))
                {
                    installedList.Add("mobile-ios");
                }
            }

            var outputJson = new Dictionary<string, string[]>()
            {
                ["installed"] = installedList.ToArray()
            };

            Reporter.Output.WriteLine("==workloadListJsonOutputStart==");
            Reporter.Output.WriteLine(
                JsonSerializer.Serialize(outputJson));
            Reporter.Output.WriteLine("==workloadListJsonOutputEnd==");
            return 0;
        }
    }
}
