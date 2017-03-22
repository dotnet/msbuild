// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static RunCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet run", args);

            result.ShowHelpOrErrorIfAppropriate();

            return result["dotnet"]["run"].Value<RunCommand>();
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            RunCommand cmd;
            
            try
            {
                cmd = FromArgs(args);
            }
            catch (CommandCreationException e)
            {
                return e.ExitCode;
            }

            return cmd.Start();
        }
    }
}
