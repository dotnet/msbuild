// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public partial class RazorSourceGenerator
    {
        private static SourceText GetProvideApplicationPartFactorySourceText()
        {
            var typeInfo = "Microsoft.AspNetCore.Mvc.ApplicationParts.ConsolidatedAssemblyApplicationPartFactory, Microsoft.AspNetCore.Mvc.Razor";
            var assemblyInfo = $@"[assembly: global::Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(""{typeInfo}"")]";
            return SourceText.From(assemblyInfo, Encoding.UTF8);
        }

        private static string GetIdentifierFromPath(string filePath)
        {
            var builder = new StringBuilder(filePath.Length);

            for (var i = 0; i < filePath.Length; i++)
            {
                switch (filePath[i])
                {
                    case ':' or '\\' or '/':
                    case char ch when !char.IsLetterOrDigit(ch):
                        builder.Append('_');
                        break;
                    default:
                        builder.Append(filePath[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        private static ParallelOptions GetParallelOptions(GeneratorExecutionContext generatorExecutionContext)
        {
            var options = new ParallelOptions { CancellationToken = generatorExecutionContext.CancellationToken };
            var isConcurrentBuild = generatorExecutionContext.Compilation.Options.ConcurrentBuild;
            if (Debugger.IsAttached || !isConcurrentBuild)
            {
                options.MaxDegreeOfParallelism = 1;
            }
            return options;
        }

        private static void HandleDebugSwitch(bool waitForDebugger)
        {
            if (waitForDebugger)
            {
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(3000);
                }
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void AssertOrFailFast(bool condition, string message)
        {
            if (!condition)
            {
                message ??= $"{nameof(AssertOrFailFast)} failed";
                var stackTrace = new StackTrace();
                Console.WriteLine(message);
                Console.WriteLine(stackTrace);

                // Use FailFast so that the process fails rudely and goes through 
                // windows error reporting (on Windows at least) for further analysis.
                if (_razorContext is not null && _razorContext.ProduceHeapDumps)
                {
                    Environment.FailFast(message);
                }
            }
            else
            {
                Debug.Assert(false, message);
            }
        }
    }
}
