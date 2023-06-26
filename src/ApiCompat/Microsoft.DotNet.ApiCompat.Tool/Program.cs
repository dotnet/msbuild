// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.PackageValidation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    class Program
    {
        static int Main(string[] args)
        {
            // CLI frontend for ApiCompat's ValidateAssemblies and ValidatePackage features.
            // Important: Keep parameters exposed in sync with the msbuild task frontend.

            // Global options
            Option<bool> generateSuppressionFileOption = new("--generate-suppression-file",
                "If true, generates a compatibility suppression file.");
            Option<string[]> suppressionFilesOption = new("--suppression-file",
                "The path to one or more suppression files to read from.")
            {
                AllowMultipleArgumentsPerToken= true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "file"
            };
            Option<string?> suppressionOutputFileOption = new("--suppression-output-file",
                "The path to a suppression file to write to when --generate-suppression-file is true.");
            Option<string?> noWarnOption = new("--noWarn",
                "A NoWarn string that allows to disable specific rules.");
            Option<bool> respectInternalsOption = new("--respect-internals",
                "If true, includes both internal and public API.");
            Option<string?> roslynAssembliesPathOption = new("--roslyn-assemblies-path",
                "The path to the directory that contains the Microsoft.CodeAnalysis assemblies.")
            {
                ArgumentHelpName = "file"
            };
            Option<MessageImportance> verbosityOption = new(new string[] { "--verbosity", "-v" },
                "Controls the log level verbosity. Allowed values are high, normal, and low.");
            verbosityOption.SetDefaultValue(MessageImportance.High);
            Option<bool> enableRuleAttributesMustMatchOption = new("--enable-rule-attributes-must-match",
                "If true, enables rule to check that attributes match.");
            Option<string[]> excludeAttributesFilesOption = new("--exclude-attributes-file",
                "The path to one or more attribute exclusion files with types in DocId format.");
            Option<bool> enableRuleCannotChangeParameterNameOption = new("--enable-rule-cannot-change-parameter-name",
                "If true, enables rule to check that the parameter names between public methods do not change.");

            // Root command
            Option<string[]> leftAssembliesOption = new(new string[] { "--left-assembly", "--left", "-l" },
                description: "The path to one or more assemblies that serve as the left side to compare.",
                parseArgument: ParseAssemblyArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                IsRequired = true
            };
            Option<string[]> rightAssembliesOption = new(new string[] { "--right-assembly", "--right", "-r" },
                description: "The path to one or more assemblies that serve as the right side to compare.",
                parseArgument: ParseAssemblyArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                IsRequired = true
            };
            Option<bool> strictModeOption = new("--strict-mode",
                "If true, performs api compatibility checks in strict mode");
            Option<string[][]?> leftAssembliesReferencesOption = new(new string[] { "--left-assembly-references", "--lref" },
                description: "Paths to assembly references or the underlying directories for a given left. Values must be separated by commas: ','.",
                parseArgument: ParseAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "file1,file2,..."
            };
            Option<string[][]?> rightAssembliesReferencesOption = new(new string[] { "--right-assembly-references", "--rref" },
                description: "Paths to assembly references or the underlying directories for a given right. Values must be separated by commas: ','.",
                parseArgument: ParseAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "file1,file2,..."
            };
            Option<bool> createWorkItemPerAssemblyOption = new("--create-work-item-per-assembly",
                "If true, enqueues a work item per passed in left and right assembly.");
            Option<(string, string)[]?> leftAssembliesTransformationPatternOption = new("--left-assemblies-transformation-pattern",
                description: "A transformation pattern for the left side assemblies.",
                parseArgument: ParseTransformationPattern)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };
            Option<(string, string)[]?> rightAssembliesTransformationPatternOption = new("--right-assemblies-transformation-pattern",
                description: "A transformation pattern for the right side assemblies.",
                parseArgument: ParseTransformationPattern)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };

            RootCommand rootCommand = new("Microsoft.DotNet.ApiCompat v" + Environment.Version.ToString(2))
            {
                TreatUnmatchedTokensAsErrors = true
            };
            rootCommand.AddGlobalOption(generateSuppressionFileOption);
            rootCommand.AddGlobalOption(suppressionFilesOption);
            rootCommand.AddGlobalOption(suppressionOutputFileOption);
            rootCommand.AddGlobalOption(noWarnOption);
            rootCommand.AddGlobalOption(respectInternalsOption);
            rootCommand.AddGlobalOption(roslynAssembliesPathOption);
            rootCommand.AddGlobalOption(verbosityOption);
            rootCommand.AddGlobalOption(enableRuleAttributesMustMatchOption);
            rootCommand.AddGlobalOption(excludeAttributesFilesOption);
            rootCommand.AddGlobalOption(enableRuleCannotChangeParameterNameOption);

            rootCommand.AddOption(leftAssembliesOption);
            rootCommand.AddOption(rightAssembliesOption);
            rootCommand.AddOption(strictModeOption);
            rootCommand.AddOption(leftAssembliesReferencesOption);
            rootCommand.AddOption(rightAssembliesReferencesOption);
            rootCommand.AddOption(createWorkItemPerAssemblyOption);
            rootCommand.AddOption(leftAssembliesTransformationPatternOption);
            rootCommand.AddOption(rightAssembliesTransformationPatternOption);

            rootCommand.SetHandler((InvocationContext context) =>
            {
                // If a roslyn assemblies path isn't provided, use the compiled against version from a subfolder.
                string roslynAssembliesPath = context.ParseResult.GetValue(roslynAssembliesPathOption) ??
                    Path.Combine(AppContext.BaseDirectory, "codeanalysis");
                RoslynResolver roslynResolver = RoslynResolver.Register(roslynAssembliesPath);

                MessageImportance verbosity = context.ParseResult.GetValue(verbosityOption);
                bool generateSuppressionFile = context.ParseResult.GetValue(generateSuppressionFileOption);
                string[]? suppressionFiles = context.ParseResult.GetValue(suppressionFilesOption);
                string? suppressionOutputFile = context.ParseResult.GetValue(suppressionOutputFileOption);
                string? noWarn = context.ParseResult.GetValue(noWarnOption);
                bool respectInternals = context.ParseResult.GetValue(respectInternalsOption);
                bool enableRuleAttributesMustMatch = context.ParseResult.GetValue(enableRuleAttributesMustMatchOption);
                string[]? excludeAttributesFiles = context.ParseResult.GetValue(excludeAttributesFilesOption);
                bool enableRuleCannotChangeParameterName = context.ParseResult.GetValue(enableRuleCannotChangeParameterNameOption);

                string[] leftAssemblies = context.ParseResult.GetValue(leftAssembliesOption)!;
                string[] rightAssemblies = context.ParseResult.GetValue(rightAssembliesOption)!;
                bool strictMode = context.ParseResult.GetValue(strictModeOption);
                string[][]? leftAssembliesReferences = context.ParseResult.GetValue(leftAssembliesReferencesOption);
                string[][]? rightAssembliesReferences = context.ParseResult.GetValue(rightAssembliesReferencesOption);
                bool createWorkItemPerAssembly = context.ParseResult.GetValue(createWorkItemPerAssemblyOption);
                (string, string)[]? leftAssembliesTransformationPattern = context.ParseResult.GetValue(leftAssembliesTransformationPatternOption);
                (string, string)[]? rightAssembliesTransformationPattern = context.ParseResult.GetValue(rightAssembliesTransformationPatternOption);

                Func<ISuppressionEngine, SuppressableConsoleLog> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidateAssemblies.Run(logFactory,
                    generateSuppressionFile,
                    suppressionFiles,
                    suppressionOutputFile,
                    noWarn,
                    respectInternals,
                    enableRuleAttributesMustMatch,
                    excludeAttributesFiles,
                    enableRuleCannotChangeParameterName,
                    leftAssemblies,
                    rightAssemblies,
                    strictMode,
                    leftAssembliesReferences,
                    rightAssembliesReferences,
                    createWorkItemPerAssembly,
                    leftAssembliesTransformationPattern,
                    rightAssembliesTransformationPattern);

                roslynResolver.Unregister();
            });

            // Package command
            Argument<string> packageArgument = new("--package",
                "The path to the package that should be validated")
            {
                Arity = ArgumentArity.ExactlyOne
            };
            Option<string?> runtimeGraphOption = new("--runtime-graph",
                "The path to the runtime graph to read from.")
            {
                ArgumentHelpName = "json"
            };
            Option<bool> runApiCompatOption = new("--run-api-compat",
                "If true, performs api compatibility checks on the package assets.");
            runApiCompatOption.SetDefaultValue(true);
            Option<bool> enableStrictModeForCompatibleTfmsOption = new("--enable-strict-mode-for-compatible-tfms",
                "Validates api compatibility in strict mode for contract and implementation assemblies for all compatible target frameworks.");
            Option<bool> enableStrictModeForCompatibleFrameworksInPackageOption = new("--enable-strict-mode-for-compatible-frameworks-in-package",
                "Validates api compatibility in strict mode for assemblies that are compatible based on their target framework.");
            Option<bool> enableStrictModeForBaselineValidationOption = new("--enable-strict-mode-for-baseline-validation",
                "Validates api compatibility in strict mode for package baseline checks.");
            Option<string?> baselinePackageOption = new("--baseline-package",
                "The path to a baseline package to validate against the current package.")
            {
                ArgumentHelpName = "nupkg"
            };
            Option<Dictionary<NuGetFramework, IEnumerable<string>>?> packageAssemblyReferencesOption = new("--package-assembly-references",
                description: "Paths to assembly references or their underlying directories for a specific target framework in the package. Values must be separated by commas: ','.",
                parseArgument: ParsePackageAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "tfm=file1,file2,..."
            };
            Option<Dictionary<NuGetFramework, IEnumerable<string>>?> baselinePackageAssemblyReferencesOption = new("--baseline-package-assembly-references",
                description: "Paths to assembly references or their underlying directories for a specific target framework in the baseline package. Values must be separated by commas: ','.",
                parseArgument: ParsePackageAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "tfm=file1,file2,..."
            };

            Command packageCommand = new("package", "Validates the compatibility of package assets");
            packageCommand.AddArgument(packageArgument);
            packageCommand.AddOption(runtimeGraphOption);
            packageCommand.AddOption(runApiCompatOption);
            packageCommand.AddOption(enableStrictModeForCompatibleTfmsOption);
            packageCommand.AddOption(enableStrictModeForCompatibleFrameworksInPackageOption);
            packageCommand.AddOption(enableStrictModeForBaselineValidationOption);
            packageCommand.AddOption(baselinePackageOption);
            packageCommand.AddOption(packageAssemblyReferencesOption);
            packageCommand.AddOption(baselinePackageAssemblyReferencesOption);
            packageCommand.SetHandler((InvocationContext context) =>
            {
                // If a roslyn assemblies path isn't provided, use the compiled against version from a subfolder.
                string roslynAssembliesPath = context.ParseResult.GetValue(roslynAssembliesPathOption) ??
                    Path.Combine(AppContext.BaseDirectory, "codeanalysis");
                RoslynResolver roslynResolver = RoslynResolver.Register(roslynAssembliesPath);

                MessageImportance verbosity = context.ParseResult.GetValue(verbosityOption);
                bool generateSuppressionFile = context.ParseResult.GetValue(generateSuppressionFileOption);
                string[]? suppressionFiles = context.ParseResult.GetValue(suppressionFilesOption);
                string? suppressionOutputFile = context.ParseResult.GetValue(suppressionOutputFileOption);
                string? noWarn = context.ParseResult.GetValue(noWarnOption);
                bool respectInternals = context.ParseResult.GetValue(respectInternalsOption);
                bool enableRuleAttributesMustMatch = context.ParseResult.GetValue(enableRuleAttributesMustMatchOption);
                string[]? excludeAttributesFiles = context.ParseResult.GetValue(excludeAttributesFilesOption);
                bool enableRuleCannotChangeParameterName = context.ParseResult.GetValue(enableRuleCannotChangeParameterNameOption);

                string package = context.ParseResult.GetValue(packageArgument);
                bool runApiCompat = context.ParseResult.GetValue(runApiCompatOption);
                bool enableStrictModeForCompatibleTfms = context.ParseResult.GetValue(enableStrictModeForCompatibleTfmsOption);
                bool enableStrictModeForCompatibleFrameworksInPackage = context.ParseResult.GetValue(enableStrictModeForCompatibleFrameworksInPackageOption);
                bool enableStrictModeForBaselineValidation = context.ParseResult.GetValue(enableStrictModeForBaselineValidationOption);
                string? baselinePackage = context.ParseResult.GetValue(baselinePackageOption);
                string? runtimeGraph = context.ParseResult.GetValue(runtimeGraphOption);
                Dictionary<NuGetFramework, IEnumerable<string>>? packageAssemblyReferences = context.ParseResult.GetValue(packageAssemblyReferencesOption);
                Dictionary<NuGetFramework, IEnumerable<string>>? baselinePackageAssemblyReferences = context.ParseResult.GetValue(baselinePackageAssemblyReferencesOption);

                Func<ISuppressionEngine, SuppressableConsoleLog> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidatePackage.Run(logFactory,
                    generateSuppressionFile,
                    suppressionFiles,
                    suppressionOutputFile,
                    noWarn,
                    respectInternals,
                    enableRuleAttributesMustMatch,
                    excludeAttributesFiles,
                    enableRuleCannotChangeParameterName,
                    package,
                    runApiCompat,
                    enableStrictModeForCompatibleTfms,
                    enableStrictModeForCompatibleFrameworksInPackage,
                    enableStrictModeForBaselineValidation,
                    baselinePackage,
                    runtimeGraph,
                    packageAssemblyReferences,
                    baselinePackageAssemblyReferences);

                roslynResolver.Unregister();
            });

            rootCommand.AddCommand(packageCommand);
            return rootCommand.Invoke(args);
        }

        private static string[][] ParseAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            List<string[]> args = new();
            foreach (Token token in argumentResult.Tokens)
            {
                args.Add(token.Value.Split(','));
            }

            return args.ToArray();
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

        private static (string CaptureGroupPattern, string ReplacementString)[]? ParseTransformationPattern(ArgumentResult argumentResult)
        {
            var patterns = new (string CaptureGroupPattern, string ReplacementPattern)[argumentResult.Tokens.Count];
            for (int i = 0; i < argumentResult.Tokens.Count; i++)
            {
                string[] parts = argumentResult.Tokens[i].Value.Split(';');
                if (parts.Length != 2)
                {
                    argumentResult.ErrorMessage = "Invalid assemblies transformation pattern. Usage: {regex-pattern};{replacement-string}";
                    continue;
                }

                patterns[i] = (parts[0], parts[1]);
            }

            return patterns;
        }

        private static Dictionary<NuGetFramework, IEnumerable<string>>? ParsePackageAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            const string invalidPackageAssemblyReferenceFormatMessage = "Invalid package assembly reference format {TargetFrameworkMoniker(+TargetPlatformMoniker)=assembly1,assembly2,assembly3,...}";

            Dictionary<NuGetFramework, IEnumerable<string>> packageAssemblyReferencesDict = new(argumentResult.Tokens.Count);
            foreach (Token token in argumentResult.Tokens)
            {
                string[] parts = token.Value.Split('=');
                if (parts.Length != 2)
                {
                    argumentResult.ErrorMessage = invalidPackageAssemblyReferenceFormatMessage;
                    continue;
                }

                string tfmInformation = parts[0];
                string referencePath = parts[1];

                string[] tfmInformationParts = tfmInformation.Split('+');
                if (tfmInformationParts.Length < 1 || tfmInformationParts.Length > 2)
                {
                    argumentResult.ErrorMessage = invalidPackageAssemblyReferenceFormatMessage;
                }

                string targetFrameworkMoniker = tfmInformationParts[0];
                string targetPlatformMoniker = tfmInformationParts.Length == 2 ?
                    tfmInformationParts[1] :
                    string.Empty;

                // The TPM is null when the assembly doesn't target a platform.
                if (targetFrameworkMoniker == string.Empty || referencePath == string.Empty)
                    continue;

                NuGetFramework nuGetFramework = NuGetFramework.ParseComponents(targetFrameworkMoniker, targetPlatformMoniker);
                // Skip duplicate frameworks which could be passed in when using TFM aliases.
                if (packageAssemblyReferencesDict.ContainsKey(nuGetFramework))
                {
                    continue;
                }

                string[] references = referencePath.Split(',');
                packageAssemblyReferencesDict.Add(nuGetFramework, references);
            }

            return packageAssemblyReferencesDict;
        }
    }
}
