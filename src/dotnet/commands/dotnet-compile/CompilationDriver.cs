// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class CompilationDriver
    {
        private readonly ICompiler _managedCompiler;
        private readonly ICompiler _nativeCompiler;

        public CompilationDriver(ICompiler managedCompiler, ICompiler nativeCompiler)
        {
            _managedCompiler = managedCompiler;
            _nativeCompiler = nativeCompiler;
        }

        public bool Compile(IEnumerable<ProjectContext> contexts, CompilerCommandApp args)
        {
            var success = true;

            foreach (var context in contexts)
            {
                success &= _managedCompiler.Compile(context, args);
                if (args.IsNativeValue && success)
                {
                    var runtimeContext = context.CreateRuntimeContext(args.GetRuntimes());
                    success &= _nativeCompiler.Compile(runtimeContext, args);
                }
            }

            return success;
        }
    }
}
