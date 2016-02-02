// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Resgen
{
    public partial class ResgenCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var help = false;
            string helpText = null;
            var returnCode = 0;

            var resgenCommand = new ResgenCommand();
            try
            {
                ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.ApplicationName = "Resource compiler";

                    syntax.HandleHelp = false;
                    syntax.HandleErrors = false;

                    syntax.DefineOption("o|output", ref resgenCommand.OutputFileName, "Output file name");
                    syntax.DefineOption("c|culture", ref resgenCommand.AssemblyCulture, "Ouput assembly culture");
                    syntax.DefineOption("v|version", ref resgenCommand.AssemblyVersion, "Ouput assembly version");
                    syntax.DefineOption("h|help", ref help, "Help for compile native.");

                    syntax.DefineOptionList("r", ref resgenCommand.CompilationReferences, "Compilation references");
                    syntax.DefineParameterList("args", ref resgenCommand.Args, "Input files");

                    helpText = syntax.GetHelpText();
                });
            }
            catch (ArgumentSyntaxException exception)
            {
                Console.Error.WriteLine(exception.Message);
                help = true;
                returnCode = 1;
            }

            if (resgenCommand.Args.Count == 0)
            {
                Reporter.Error.WriteLine("No input files specified");
                help = true;
                returnCode = 1;
            }

            if (help)
            {
                Console.WriteLine(helpText);
                return returnCode;
            }

            try
            {
                return resgenCommand.Execute();
            }
            catch (Exception ex)
            {
#if DEBUG
                Reporter.Error.WriteLine(ex.ToString());
#else
                Reporter.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

    }
}
