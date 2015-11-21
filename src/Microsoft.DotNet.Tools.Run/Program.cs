// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.CommandLine;

namespace Microsoft.DotNet.Tools.Run
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            RunCommand runCmd = new RunCommand();

            ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.HandleErrors = false;
                syntax.DefineOption("f|framework", ref runCmd.Framework, "Compile a specific framework");
                syntax.DefineOption("c|configuration", ref runCmd.Configuration, "Configuration under which to build");
                syntax.DefineOption("t|preserve-temporary", ref runCmd.PreserveTemporary, "Keep the output's temporary directory around");
                syntax.DefineOption("p|project", ref runCmd.Project, "The path to the project to run (defaults to the current directory). Can be a path to a project.json or a project directory");

                // TODO: this is not supporting args which can be switches (i.e. --test)
                // TODO: we need to make a change in System.CommandLine or parse args ourselves.
                syntax.DefineParameterList("args", ref runCmd.Args, "Arguments to pass to the executable or script");
            });

            try
            {
                return runCmd.Start();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }
    }
}
