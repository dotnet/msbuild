// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class CommpileCommand
    {

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            try
            {
                var commandFactory = new DotNetCommandFactory();
                var scriptRunner = new ScriptRunner();
                var managedCompiler = new ManagedCompiler(scriptRunner, commandFactory);
                var nativeCompiler = new NativeCompiler();
                var compilationDriver = new CompilationDriver(managedCompiler, nativeCompiler);

                var compilerCommandArgs = new CompilerCommandApp("dotnet compile", ".NET Compiler", "Compiler for the .NET Platform");

                return compilerCommandArgs.Execute(compilationDriver.Compile, args);
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
