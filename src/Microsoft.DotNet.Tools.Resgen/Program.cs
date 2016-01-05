// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Resgen
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(false);
            app.Name = "resgen";
            app.FullName = "Resource compiler";
            app.Description = "Microsoft (R) .NET Resource Generator";
            app.HelpOption("-h|--help");

            var ouputFile = app.Option("-o", "Output file name", CommandOptionType.SingleValue);
            var culture = app.Option("-c", "Ouput assembly culture", CommandOptionType.SingleValue);
            var version = app.Option("-v", "Ouput assembly version", CommandOptionType.SingleValue);
            var references = app.Option("-r", "Compilation references", CommandOptionType.MultipleValue);
            var inputFiles = app.Argument("<input>", "Input files", true);

            app.OnExecute(() =>
            {
                if (!inputFiles.Values.Any())
                {
                    Reporter.Error.WriteLine("No input files specified");
                    return 1;
                }

                var intputResourceFiles = inputFiles.Values.Select(ParseInputFile).ToArray();
                var outputResourceFile = ResourceFile.Create(ouputFile.Value());

                switch (outputResourceFile.Type)
                {
                    case ResourceFileType.Dll:
                        using (var outputStream = outputResourceFile.File.Create())
                        {
                            var metadata = new AssemblyInfoOptions();
                            metadata.Culture = culture.Value();
                            metadata.AssemblyVersion = version.Value();

                            ResourceAssemblyGenerator.Generate(intputResourceFiles,
                                outputStream,
                                metadata,
                                Path.GetFileNameWithoutExtension(outputResourceFile.File.Name),
                                references.Values.ToArray()
                                );
                        }
                        break;
                    case ResourceFileType.Resources:
                        using (var outputStream = outputResourceFile.File.Create())
                        {
                            if (intputResourceFiles.Length > 1)
                            {
                                Reporter.Error.WriteLine("Only one input file required when generating .resource output");
                                return 1;
                            }
                            ResourcesFileGenerator.Generate(intputResourceFiles.Single().Resource, outputStream);
                        }
                        break;
                    default:
                        Reporter.Error.WriteLine("Resx output type not supported");
                        return 1;
                }

                return 0;
            });

            try
            {
                return app.Execute(args);
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

        private static ResourceSource ParseInputFile(string arg)
        {
            var seperatorIndex = arg.IndexOf(',');
            string name = null;
            string metadataName = null;
            if (seperatorIndex > 0)
            {
                name = arg.Substring(0, seperatorIndex);
                metadataName = arg.Substring(seperatorIndex + 1);
            }
            else
            {
                name = arg;
                metadataName = arg;
            }
            return new ResourceSource(ResourceFile.Create(name), metadataName);
        }
    }
}
