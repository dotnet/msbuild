// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.GenAPI.Tool
{
    /// <summary>
    /// CLI frontend for the Roslyn-based GenAPI.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            // Global options
            Option<string[]> assembliesOption = new("--assembly",
                description: "The path to one or more assemblies or directories with assemblies.",
                parseArgument: ParseAssemblyArgument)
            {
                Arity = ArgumentArity.OneOrMore,
                IsRequired = true
            };

            Option<string[]?> assemblyReferencesOption = new("--assembly-reference",
                description: "Paths to assembly references or their underlying directories for a specific target framework in the package.",
                parseArgument: ParseAssemblyArgument)
            {
                Arity = ArgumentArity.ZeroOrMore
            };

            Option<string[]?> excludeApiFilesOption = new("--exclude-api-file",
                description: "The path to one or more api exclusion files with types in DocId format.",
                parseArgument: ParseAssemblyArgument)
            {
                Arity = ArgumentArity.ZeroOrMore
            };

            Option<string[]?> excludeAttributesFilesOption = new("--exclude-attributes-file",
                description: "The path to one or more attribute exclusion files with types in DocId format.",
                parseArgument: ParseAssemblyArgument)
            {
                Arity = ArgumentArity.ZeroOrMore
            };

            Option<string?> outputPathOption = new("--output-path",
                @"Output path. Default is the console. Can specify an existing directory as well
            and then a file will be created for each assembly with the matching name of the assembly.");

            Option<string?> headerFileOption = new("--header-file",
                "Specify a file with an alternate header content to prepend to output.");

            Option<string?> exceptionMessageOption = new("--exception-message",
                "If specified - method bodies should throw PlatformNotSupportedException, else `throw null`.");

            Option<bool> respectInternalsOption = new("--respect-internals",
                "If true, includes both internal and public API.");

            Option<bool> includeAssemblyAttributesOption = new("--include-assembly-attributes",
                "Includes assembly attributes which are values that provide information about an assembly. Default is false.");

            RootCommand rootCommand = new("Microsoft.DotNet.GenAPI")
            {
                TreatUnmatchedTokensAsErrors = true
            };
            rootCommand.AddGlobalOption(assembliesOption);
            rootCommand.AddGlobalOption(assemblyReferencesOption);
            rootCommand.AddGlobalOption(excludeApiFilesOption);
            rootCommand.AddGlobalOption(excludeAttributesFilesOption);
            rootCommand.AddGlobalOption(outputPathOption);
            rootCommand.AddGlobalOption(headerFileOption);
            rootCommand.AddGlobalOption(exceptionMessageOption);
            rootCommand.AddGlobalOption(respectInternalsOption);
            rootCommand.AddGlobalOption(includeAssemblyAttributesOption);

            rootCommand.SetHandler((InvocationContext context) =>
            {
                GenAPIApp.Run(new ConsoleLog(MessageImportance.Normal), new GenAPIApp.Context(
                    context.ParseResult.GetValue(assembliesOption)!,
                    context.ParseResult.GetValue(assemblyReferencesOption),
                    context.ParseResult.GetValue(outputPathOption),
                    context.ParseResult.GetValue(headerFileOption),
                    context.ParseResult.GetValue(exceptionMessageOption),
                    context.ParseResult.GetValue(excludeApiFilesOption),
                    context.ParseResult.GetValue(excludeAttributesFilesOption),
                    context.ParseResult.GetValue(respectInternalsOption),
                    context.ParseResult.GetValue(includeAssemblyAttributesOption)
                ));
            });

            return rootCommand.Invoke(args);
        }

        private static string[] ParseAssemblyArgument(ArgumentResult argumentResult)
        {
            List<string> args = new();
            foreach (Token token in argumentResult.Tokens)
            {
                args.AddRange(token.Value.Split(','));
            }

            return args.ToArray();
        }
    }
}

