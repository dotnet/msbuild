// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Clean
{
    public class CleanCommand : MSBuildForwardingApp
    {
        public CleanCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static CleanCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var msbuildArgs = new List<string>
            {
                "-verbosity:normal"
            };

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet clean", args);

            result.ShowHelpOrErrorIfAppropriate();

            var parsedClean = result["dotnet"]["clean"];

            msbuildArgs.AddRange(parsedClean.Arguments);

            msbuildArgs.Add("-target:Clean");

            msbuildArgs.AddRange(parsedClean.OptionValuesToBeForwarded());

            return new CleanCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
