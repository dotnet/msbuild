// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Resgen
{
    public partial class ResgenCommand
    {
        public static int Run(IEnumerable<string> inputFiles, string culture, string outputFile, string version, IEnumerable<string> compilationReferences)
        {
            if (!inputFiles.Any())
            {
                Reporter.Error.WriteLine("No input files specified");
                return 1;
            }

            ResgenCommand resgenCommand = new ResgenCommand();

            resgenCommand.Args = inputFiles;
            resgenCommand.OutputFileName = outputFile;
            resgenCommand.AssemblyCulture = culture;
            resgenCommand.AssemblyVersion = version;
            resgenCommand.CompilationReferences = compilationReferences;

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
