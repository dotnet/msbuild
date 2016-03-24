// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.Dotnet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Restore
{
    public partial class RestoreCommand
    {
        private static readonly string DefaultRid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(false)
            {
                Name = "dotnet restore",
                FullName = ".NET project dependency restorer",
                Description = "Restores dependencies listed in project.json"
            };

            // Parse --quiet, because we have to handle that specially since NuGet3 has a different
            // "--verbosity" switch that goes BEFORE the command
            var quiet = args.Any(s => s.Equals("--quiet", StringComparison.OrdinalIgnoreCase));
            args = args.Where(s => !s.Equals("--quiet", StringComparison.OrdinalIgnoreCase)).ToArray();

            // Always infer runtimes in dotnet-restore (for now).
            if (!args.Any(s => s.Equals("--infer-runtimes", StringComparison.OrdinalIgnoreCase)))
            {
                args = Enumerable.Concat(new [] { "--infer-runtimes" }, args).ToArray();
            }

            app.OnExecute(() =>
            {
                try
                {
                    return NuGet3.Restore(args, quiet);
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine(e.Message);

                    return -1;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);

                    return -2;
                }
            });

            return app.Execute(args);
        }
    }
}
