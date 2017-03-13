// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Cli;
using System.Diagnostics;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Pack
{
    public class PackCommand : MSBuildForwardingApp
    {
        public PackCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
        }

        public static PackCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet pack", args);

            result.ShowHelpOrErrorIfAppropriate();

            var parsedPack = result["dotnet"]["pack"];
          
            var msbuildArgs = new List<string>()
            {
                    "/t:pack"
            };

            msbuildArgs.AddRange(parsedPack.OptionValuesToBeForwarded());

            msbuildArgs.AddRange(parsedPack.Arguments);

            return new PackCommand(msbuildArgs, msbuildPath);
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            PackCommand cmd;
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
