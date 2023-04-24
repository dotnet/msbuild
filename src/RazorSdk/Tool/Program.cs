// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            // To minimize the size of the SDK, we resolve all Rosyln-related assemblies
            // from the `Roslyn/bincore` location in the SDK. The `RegisterAssemblyResolutionEvents`
            // method registers the event that will handle loading the assemblies from the correct
            // path. Note: since assembly resolution starts immediately when the `Main` method is
            // invoked, so we register the event listener here to ensure they are registered before
            // we `Main` is invoked.
            RegisterAssemblyResolutionEvents();
            return RunApplication(args);
        }

        private static int RunApplication(string[] args)
        {
            DebugMode.HandleDebugSwitch(ref args);

            var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => { cancel.Cancel(); };

            var outputWriter = new StringWriter();
            var errorWriter = new StringWriter();

            // Prevent shadow copying.
            var loader = new DefaultExtensionAssemblyLoader(baseDirectory: null);
            var checker = new DefaultExtensionDependencyChecker(loader, outputWriter, errorWriter);

            var application = new Application(
                cancel.Token,
                loader,
                checker,
                (path, properties) => MetadataReference.CreateFromFile(path, properties),
                outputWriter,
                errorWriter);

            var result = application.Execute(args);

            var output = outputWriter.ToString();
            var error = errorWriter.ToString();

            outputWriter.Dispose();
            errorWriter.Dispose();

            Console.Write(output);
            Console.Error.Write(error);

            // This will no-op if server logging is not enabled.
            ServerLogger.Log(output);
            ServerLogger.Log(error);

            return result;
        }

        private static void RegisterAssemblyResolutionEvents()
        {
            var roslynPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Roslyn", "bincore");

            AssemblyLoadContext.Default.Resolving += (context, assembly) =>
            {
                if (assembly.Name is "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp")
                {
                    var loadedAssembly = context.LoadFromAssemblyPath(Path.Combine(roslynPath, assembly.Name + ".dll"));
                    // Avoid scenarioes where the assembly in rosylnPath is older than what we expect
                    if (loadedAssembly.GetName().Version < assembly.Version)
                    {
                        throw new Exception($"Found a version of {assembly.Name} that was lower than the target version of {assembly.Version}");
                    }
                    return loadedAssembly;
                }
                return null;
            };
        }
    }
}
