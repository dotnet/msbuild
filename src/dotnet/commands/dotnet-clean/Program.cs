// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using System.Diagnostics;

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
            var msbuildArgs = new List<string>();

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet clean", args);

            Reporter.Output.WriteLine(result.Diagram());

            result.ShowHelpIfRequested();

            var parsedClean = result["dotnet"]["clean"];
                
            msbuildArgs.Add("/t:Clean");

            msbuildArgs.AddRange(appliedBuildOptions.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(parsedClean.Arguments);

            return new CleanCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CleanCommand cmd;
            try
            {
                cmd = FromArgs(args);
            }
            catch (CommandCreationException e)
            {
                return e.ExitCode;
            }

            return cmd.Execute();
        }
    }
}
