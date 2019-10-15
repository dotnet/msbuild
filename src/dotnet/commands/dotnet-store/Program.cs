// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using System.Collections.Generic;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Store
{
    public class StoreCommand : MSBuildForwardingApp
    {
        private StoreCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static StoreCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet store", args);

            result.ShowHelpOrErrorIfAppropriate();

            var appliedBuildOptions = result["dotnet"]["store"];

            if (!appliedBuildOptions.HasOption("-m"))
            {
                throw new GracefulException(LocalizableStrings.SpecifyManifests);
            }

            msbuildArgs.Add("-target:ComposeStore");

            msbuildArgs.AddRange(appliedBuildOptions.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(appliedBuildOptions.Arguments);

            return new StoreCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
