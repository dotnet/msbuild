// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Resgen
{
    public partial class ResgenCommand
    {
        public string OutputFileName = null;
        public string AssemblyCulture = null;
        public string AssemblyVersion = null;
        public IReadOnlyList<string> CompilationReferences = null;
        public IReadOnlyList<string> Args = null;

        public int Execute()
        {
            var inputResourceFiles = Args.Select(ParseInputFile).ToArray();
            var outputResourceFile = ResourceFile.Create(OutputFileName.Trim('"'));

            var trimmedCompilationReferences = default(string[]);
            if (CompilationReferences != null)
            {
                trimmedCompilationReferences = CompilationReferences.Select(r => r.Trim('"')).ToArray();
            }

            switch (outputResourceFile.Type)
            {
                case ResourceFileType.Dll:
                    using (var outputStream = outputResourceFile.File.Create())
                    {
                        var metadata = new AssemblyInfoOptions
                        {
                            Culture = AssemblyCulture,
                            AssemblyVersion = AssemblyVersion,
                        };

                        ResourceAssemblyGenerator.Generate(inputResourceFiles,
                            outputStream,
                            metadata,
                            Path.GetFileNameWithoutExtension(outputResourceFile.File.Name),
                            trimmedCompilationReferences
                            );
                    }
                    break;
                case ResourceFileType.Resources:
                    using (var outputStream = outputResourceFile.File.Create())
                    {
                        if (inputResourceFiles.Length > 1)
                        {
                            Reporter.Error.WriteLine("Only one input file required when generating .resource output");
                            return 1;
                        }
                        ResourcesFileGenerator.Generate(inputResourceFiles.Single().Resource, outputStream);
                    }
                    break;
                default:
                    Reporter.Error.WriteLine("Resx output type not supported");
                    return 1;
            }

            return 0;
        }

        private static ResourceSource ParseInputFile(string arg)
        {
            var separatorIndex = arg.IndexOf(',');
            string name;
            string metadataName;
            if (separatorIndex > 0)
            {
                name = arg.Substring(0, separatorIndex);
                metadataName = arg.Substring(separatorIndex + 1);
            }
            else
            {
                name = arg;
                metadataName = arg;
            }

            // Remove surrounding quotes
            name = name.Trim('"');
            metadataName = metadataName.Trim('"');

            return new ResourceSource(ResourceFile.Create(name), metadataName);
        }
    }
}
