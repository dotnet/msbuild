// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class NativeCompiler : Compiler
    {
        public override bool Compile(ProjectContext context, CompilerCommandApp args)
        {
            var outputPaths = context.GetOutputPaths(args.ConfigValue, args.BuildBasePathValue, args.OutputValue);
            var outputPath = outputPaths.RuntimeOutputPath;
            var nativeOutputPath = Path.Combine(outputPath, "native");
            var intermediateOutputPath =
                outputPaths.IntermediateOutputDirectoryPath;
            var nativeTempOutput = Path.Combine(intermediateOutputPath, "native");
            Directory.CreateDirectory(nativeOutputPath);
            Directory.CreateDirectory(nativeTempOutput);

            var managedOutput = outputPaths.CompilationFiles.Assembly;

            // Create the library exporter
            var exporter = context.CreateExporter(args.ConfigValue);
            var exports = exporter.GetDependencies();
 
            // Runtime assemblies.
            // TODO: native assets/resources.
            var references = exports
                .SelectMany(export => export.RuntimeAssemblyGroups.GetDefaultAssets())
                .Select(r => r.ResolvedPath)
                .ToList();

            // Setup native args.
            var nativeArgs = new List<string>();

            // Input Assembly
            nativeArgs.Add($"{managedOutput}");

            // Add Resolved Assembly References
            foreach (var reference in references)
            {
                nativeArgs.Add("--reference");
                nativeArgs.Add(reference);
            }

            // ILC Args
            foreach (var ilcArg in args.IlcArgsValue)
            {
                nativeArgs.Add("--ilcarg");
                nativeArgs.Add($"\"{ilcArg}\"");
            }

            // ILC Path
            if (!string.IsNullOrWhiteSpace(args.IlcPathValue))
            {
                nativeArgs.Add("--ilcpath");
                nativeArgs.Add(args.IlcPathValue);
            }

            // ILC SDK Path
            if (!string.IsNullOrWhiteSpace(args.IlcSdkPathValue))
            {
                nativeArgs.Add("--ilcsdkpath");
                nativeArgs.Add(args.IlcSdkPathValue);
            }

            // AppDep SDK Path
            if (!string.IsNullOrWhiteSpace(args.AppDepSdkPathValue))
            {
                nativeArgs.Add("--appdepsdk");
                nativeArgs.Add(args.AppDepSdkPathValue);
            }

            // CodeGen Mode
            if (args.IsCppModeValue)
            {
                nativeArgs.Add("--mode");
                nativeArgs.Add("cpp");
            }

            if (!string.IsNullOrWhiteSpace(args.CppCompilerFlagsValue))
            {
                nativeArgs.Add("--cppcompilerflags");
                nativeArgs.Add(args.CppCompilerFlagsValue);
            }

            // Configuration
            if (args.ConfigValue != null)
            {
                nativeArgs.Add("--configuration");
                nativeArgs.Add(args.ConfigValue);
            }

            // Architecture
            if (args.ArchValue != null)
            {
                nativeArgs.Add("--arch");
                nativeArgs.Add(args.ArchValue);
            }

            // Intermediate Path
            nativeArgs.Add("--temp-output");
            nativeArgs.Add($"{nativeTempOutput}");

            // Output Path
            nativeArgs.Add("--output");
            nativeArgs.Add($"{nativeOutputPath}");

            // Write Response File
            var rsp = Path.Combine(nativeTempOutput, $"dotnet-compile-native.{context.ProjectFile.Name}.rsp");
            File.WriteAllLines(rsp, nativeArgs);

            // TODO Add -r assembly.dll for all Nuget References
            //     Need CoreRT Framework published to nuget

            // Do Native Compilation
            var result = Native.CompileNativeCommand.Run(new string[] { "--rsp", $"{rsp}" });

            return result == 0;
        }
    }
}
