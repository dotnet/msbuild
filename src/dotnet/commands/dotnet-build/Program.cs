// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Build
{
    public class BuildCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            try
            {
                var app = new BuilderCommandApp("dotnet build", ".NET Builder", "Builder for the .NET Platform. It performs incremental compilation if it's safe to do so. Otherwise it delegates to dotnet-compile which performs non-incremental compilation");
                return app.Execute(OnExecute, args);
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

        private static bool OnExecute(List<ProjectContext> contexts, CompilerCommandApp args)
        {
            var compileContexts = contexts.Select(context => new CompileContext(context, (BuilderCommandApp)args)).ToList();

            var incrementalSafe = compileContexts.All(c => c.IsSafeForIncrementalCompilation);

            return compileContexts.All(c => c.Compile(incrementalSafe));
        }
    }
}
