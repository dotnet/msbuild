// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
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
            CliOption<bool> generateSuppressionFileOption = new("--generate-suppression-file")
            {
                Description = "If true, generates a compatibility suppression file.",
                Recursive = true
            };
            CliOption<bool> preserveUnnecessarySuppressionsOption = new("--preserve-unnecessary-suppressions")
            {
                Description = "If true, preserves unnecessary suppressions when re-generating the suppression file.",
                Recursive = true
            };
            CliOption<bool> permitUnnecessarySuppressionsOption = new("--permit-unnecessary-suppressions")
            {
                Description = "If true, permits unnecessary suppressions in the suppression file.",
                Recursive = true
            };
            CliOption<string[]> suppressionFilesOption = new("--suppression-file")
            {
                Description = "The path to one or more suppression files to read from.",
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                HelpName = "file",
                Recursive = true
            };
            CliOption<string?> suppressionOutputFileOption = new("--suppression-output-file")
            {
                Description = "The path to a suppression file to write to when --generate-suppression-file is true.",
                Recursive = true
            };
            CliOption<string?> noWarnOption = new("--noWarn")
            {
                Description = "A NoWarn string that allows to disable specific rules.",
                Recursive = true
            };
            CliOption<bool> respectInternalsOption = new("--respect-internals")
            {
                Description = "If true, includes both internal and public API.",
                Recursive = true
            };
            CliOption<string?> roslynAssembliesPathOption = new("--roslyn-assemblies-path")
            {
                Description = "The path to the directory that contains the Microsoft.CodeAnalysis assemblies.",
                HelpName = "file",
                Recursive = true
            };
            CliOption<MessageImportance> verbosityOption = new("--verbosity", "-v")
            {
                Description = "Controls the log level verbosity. Allowed values are high, normal, and low.",
                DefaultValueFactory = _ => MessageImportance.Normal,
                Recursive = true
            };
            CliOption<bool> enableRuleAttributesMustMatchOption = new("--enable-rule-attributes-must-match")
            {
                Description = "If true, enables rule to check that attributes match.",
                Recursive = true
            };
            CliOption<string[]> excludeAttributesFilesOption = new("--exclude-attributes-file")
            {
                Description = "The path to one or more attribute exclusion files with types in DocId format.",
                Recursive = true
            };
            CliOption<bool> enableRuleCannotChangeParameterNameOption = new("--enable-rule-cannot-change-parameter-name")
            {
                Description = "If true, enables rule to check that the parameter names between public methods do not change.",
                Recursive = true
            };

            // Root command
            CliOption<string[]> leftAssembliesOption = new("--left-assembly", "--left", "-l")
            {
                Description = "The path to one or more assemblies that serve as the left side to compare.",
                CustomParser = ParseAssemblyArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                Required = true
            };
            CliOption<string[]> rightAssembliesOption = new("--right-assembly", "--right", "-r")
            {
                Description = "The path to one or more assemblies that serve as the right side to compare.",
                CustomParser = ParseAssemblyArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                Required = true
            };
            CliOption<bool> strictModeOption = new("--strict-mode")
            {
                Description = "If true, performs api compatibility checks in strict mode"
            };
            CliOption<string[][]?> leftAssembliesReferencesOption = new("--left-assembly-references", "--lref")

            {
                Description = "Paths to assembly references or the underlying directories for a given left. Values must be separated by commas: ','.",
                CustomParser = ParseAssemblyReferenceArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                HelpName = "file1,file2,..."
            };
            CliOption<string[][]?> rightAssembliesReferencesOption = new("--right-assembly-references", "--rref")
            {
                Description = "Paths to assembly references or the underlying directories for a given right. Values must be separated by commas: ','.",
                CustomParser = ParseAssemblyReferenceArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                HelpName = "file1,file2,..."
            };
            CliOption<bool> createWorkItemPerAssemblyOption = new("--create-work-item-per-assembly")
            {
                Description = "If true, enqueues a work item per passed in left and right assembly."
            };
            CliOption<(string, string)[]?> leftAssembliesTransformationPatternOption = new("--left-assemblies-transformation-pattern")
            {
                Description = "A transformation pattern for the left side assemblies.",
                CustomParser = ParseTransformationPattern,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };
            CliOption<(string, string)[]?> rightAssembliesTransformationPatternOption = new("--right-assemblies-transformation-pattern")
            {
                Description = "A transformation pattern for the right side assemblies.",
                CustomParser = ParseTransformationPattern,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };

            CliRootCommand rootCommand = new("Microsoft.DotNet.ApiCompat v" + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion)
            {
                TreatUnmatchedTokensAsErrors = true
            };
            rootCommand.Options.Add(generateSuppressionFileOption);
            rootCommand.Options.Add(preserveUnnecessarySuppressionsOption);
            rootCommand.Options.Add(permitUnnecessarySuppressionsOption);
            rootCommand.Options.Add(suppressionFilesOption);
            rootCommand.Options.Add(suppressionOutputFileOption);
            rootCommand.Options.Add(noWarnOption);
            rootCommand.Options.Add(respectInternalsOption);
            rootCommand.Options.Add(roslynAssembliesPathOption);
            rootCommand.Options.Add(verbosityOption);
            rootCommand.Options.Add(enableRuleAttributesMustMatchOption);
            rootCommand.Options.Add(excludeAttributesFilesOption);
            rootCommand.Options.Add(enableRuleCannotChangeParameterNameOption);

            rootCommand.Options.Add(leftAssembliesOption);
            rootCommand.Options.Add(rightAssembliesOption);
            rootCommand.Options.Add(strictModeOption);
            rootCommand.Options.Add(leftAssembliesReferencesOption);
            rootCommand.Options.Add(rightAssembliesReferencesOption);
            rootCommand.Options.Add(createWorkItemPerAssemblyOption);
            rootCommand.Options.Add(leftAssembliesTransformationPatternOption);
            rootCommand.Options.Add(rightAssembliesTransformationPatternOption);

            rootCommand.SetAction((ParseResult parseResult) =>
            {
                // If a roslyn assemblies path isn't provided, use the compiled against version from a subfolder.
                string roslynAssembliesPath = parseResult.GetValue(roslynAssembliesPathOption) ??
                    Path.Combine(AppContext.BaseDirectory, "codeanalysis");
                RoslynResolver roslynResolver = RoslynResolver.Register(roslynAssembliesPath);

                MessageImportance verbosity = parseResult.GetValue(verbosityOption);
                bool generateSuppressionFile = parseResult.GetValue(generateSuppressionFileOption);
                bool preserveUnnecessarySuppressions = parseResult.GetValue(preserveUnnecessarySuppressionsOption);
                bool permitUnnecessarySuppressions = parseResult.GetValue(permitUnnecessarySuppressionsOption);
                string[]? suppressionFiles = parseResult.GetValue(suppressionFilesOption);
                string? suppressionOutputFile = parseResult.GetValue(suppressionOutputFileOption);
                string? noWarn = parseResult.GetValue(noWarnOption);
                bool respectInternals = parseResult.GetValue(respectInternalsOption);
                bool enableRuleAttributesMustMatch = parseResult.GetValue(enableRuleAttributesMustMatchOption);
                string[]? excludeAttributesFiles = parseResult.GetValue(excludeAttributesFilesOption);
                bool enableRuleCannotChangeParameterName = parseResult.GetValue(enableRuleCannotChangeParameterNameOption);

                string[] leftAssemblies = parseResult.GetValue(leftAssembliesOption)!;
                string[] rightAssemblies = parseResult.GetValue(rightAssembliesOption)!;
                bool strictMode = parseResult.GetValue(strictModeOption);
                string[][]? leftAssembliesReferences = parseResult.GetValue(leftAssembliesReferencesOption);
                string[][]? rightAssembliesReferences = parseResult.GetValue(rightAssembliesReferencesOption);
                bool createWorkItemPerAssembly = parseResult.GetValue(createWorkItemPerAssemblyOption);
                (string, string)[]? leftAssembliesTransformationPattern = parseResult.GetValue(leftAssembliesTransformationPatternOption);
                (string, string)[]? rightAssembliesTransformationPattern = parseResult.GetValue(rightAssembliesTransformationPatternOption);

                Func<ISuppressionEngine, SuppressibleConsoleLog> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidateAssemblies.Run(logFactory,
                    generateSuppressionFile,
                    preserveUnnecessarySuppressions,
                    permitUnnecessarySuppressions,
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
            CliArgument<string> packageArgument = new("--package")
            {
                Description = "The path to the package that should be validated",
                Arity = ArgumentArity.ExactlyOne
            };
            CliOption<string?> runtimeGraphOption = new("--runtime-graph")
            {
                Description = "The path to the runtime graph to read from.",
                HelpName = "json"
            };
            CliOption<bool> runApiCompatOption = new("--run-api-compat")
            {
                Description = "If true, performs api compatibility checks on the package assets.",
                DefaultValueFactory = _ => true
            };
            CliOption<bool> enableStrictModeForCompatibleTfmsOption = new("--enable-strict-mode-for-compatible-tfms")
            {
                Description = "Validates api compatibility in strict mode for contract and implementation assemblies for all compatible target frameworks.",
                DefaultValueFactory = _ => true
            };
            CliOption<bool> enableStrictModeForCompatibleFrameworksInPackageOption = new("--enable-strict-mode-for-compatible-frameworks-in-package")
            {
                Description = "Validates api compatibility in strict mode for assemblies that are compatible based on their target framework."
            };
            CliOption<bool> enableStrictModeForBaselineValidationOption = new("--enable-strict-mode-for-baseline-validation")
            {
                Description = "Validates api compatibility in strict mode for package baseline checks."
            };
            CliOption<string?> baselinePackageOption = new("--baseline-package")
            {
                Description = "The path to a baseline package to validate against the current package.",
                HelpName = "nupkg"
            };
            CliOption<Dictionary<NuGetFramework, IEnumerable<string>>?> packageAssemblyReferencesOption = new("--package-assembly-references")
            {
                Description = "Paths to assembly references or their underlying directories for a specific target framework in the package. Values must be separated by commas: ','.",
                CustomParser = ParsePackageAssemblyReferenceArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                HelpName = "tfm=file1,file2,..."
            };
            CliOption<Dictionary<NuGetFramework, IEnumerable<string>>?> baselinePackageAssemblyReferencesOption = new("--baseline-package-assembly-references")
            {
                Description = "Paths to assembly references or their underlying directories for a specific target framework in the baseline package. Values must be separated by commas: ','.",
                CustomParser = ParsePackageAssemblyReferenceArgument,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                HelpName = "tfm=file1,file2,..."
            };

            CliCommand packageCommand = new("package", "Validates the compatibility of package assets");
            packageCommand.Arguments.Add(packageArgument);
            packageCommand.Options.Add(runtimeGraphOption);
            packageCommand.Options.Add(runApiCompatOption);
            packageCommand.Options.Add(enableStrictModeForCompatibleTfmsOption);
            packageCommand.Options.Add(enableStrictModeForCompatibleFrameworksInPackageOption);
            packageCommand.Options.Add(enableStrictModeForBaselineValidationOption);
            packageCommand.Options.Add(baselinePackageOption);
            packageCommand.Options.Add(packageAssemblyReferencesOption);
            packageCommand.Options.Add(baselinePackageAssemblyReferencesOption);
            packageCommand.SetAction((ParseResult parseResult) =>
            {
                // If a roslyn assemblies path isn't provided, use the compiled against version from a subfolder.
                string roslynAssembliesPath = parseResult.GetValue(roslynAssembliesPathOption) ??
                    Path.Combine(AppContext.BaseDirectory, "codeanalysis");
                RoslynResolver roslynResolver = RoslynResolver.Register(roslynAssembliesPath);

                MessageImportance verbosity = parseResult.GetValue(verbosityOption);
                bool generateSuppressionFile = parseResult.GetValue(generateSuppressionFileOption);
                bool preserveUnnecessarySuppressions = parseResult.GetValue(preserveUnnecessarySuppressionsOption);
                bool permitUnnecessarySuppressions = parseResult.GetValue(permitUnnecessarySuppressionsOption);
                string[]? suppressionFiles = parseResult.GetValue(suppressionFilesOption);
                string? suppressionOutputFile = parseResult.GetValue(suppressionOutputFileOption);
                string? noWarn = parseResult.GetValue(noWarnOption);
                bool respectInternals = parseResult.GetValue(respectInternalsOption);
                bool enableRuleAttributesMustMatch = parseResult.GetValue(enableRuleAttributesMustMatchOption);
                string[]? excludeAttributesFiles = parseResult.GetValue(excludeAttributesFilesOption);
                bool enableRuleCannotChangeParameterName = parseResult.GetValue(enableRuleCannotChangeParameterNameOption);

                string? package = parseResult.GetValue(packageArgument);
                bool runApiCompat = parseResult.GetValue(runApiCompatOption);
                bool enableStrictModeForCompatibleTfms = parseResult.GetValue(enableStrictModeForCompatibleTfmsOption);
                bool enableStrictModeForCompatibleFrameworksInPackage = parseResult.GetValue(enableStrictModeForCompatibleFrameworksInPackageOption);
                bool enableStrictModeForBaselineValidation = parseResult.GetValue(enableStrictModeForBaselineValidationOption);
                string? baselinePackage = parseResult.GetValue(baselinePackageOption);
                string? runtimeGraph = parseResult.GetValue(runtimeGraphOption);
                Dictionary<NuGetFramework, IEnumerable<string>>? packageAssemblyReferences = parseResult.GetValue(packageAssemblyReferencesOption);
                Dictionary<NuGetFramework, IEnumerable<string>>? baselinePackageAssemblyReferences = parseResult.GetValue(baselinePackageAssemblyReferencesOption);

                Func<ISuppressionEngine, SuppressibleConsoleLog> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidatePackage.Run(logFactory,
                    generateSuppressionFile,
                    preserveUnnecessarySuppressions,
                    permitUnnecessarySuppressions,
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

            rootCommand.Subcommands.Add(packageCommand);
            return rootCommand.Parse(args).Invoke();
        }

        private static string[][] ParseAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            List<string[]> args = new();
            foreach (var token in argumentResult.Tokens)
            {
                args.Add(token.Value.Split(','));
            }

            return args.ToArray();
        }

        private static string[] ParseAssemblyArgument(ArgumentResult argumentResult)
        {
            List<string> args = new();
            foreach (var token in argumentResult.Tokens)
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
                    argumentResult.AddError("Invalid assemblies transformation pattern. Usage: {regex-pattern};{replacement-string}");
                    continue;
                }

                patterns[i] = (parts[0], parts[1]);
            }

            return patterns;
        }

        private static Dictionary<NuGetFramework, IEnumerable<string>>? ParsePackageAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            const string invalidPackageAssemblyReferenceFormatMessage = "Invalid package assembly reference format {TargetFrameworkMoniker(+TargetPlatformMoniker)|assembly1,assembly2,assembly3,...}";

            Dictionary<NuGetFramework, IEnumerable<string>> packageAssemblyReferencesDict = new(argumentResult.Tokens.Count);
            foreach (var token in argumentResult.Tokens)
            {
                string[] parts = token.Value.Split('|');
                if (parts.Length != 2)
                {
                    argumentResult.AddError(invalidPackageAssemblyReferenceFormatMessage);
                    continue;
                }

                string tfmInformation = parts[0];
                string referencePath = parts[1];

                string[] tfmInformationParts = tfmInformation.Split('+');
                if (tfmInformationParts.Length < 1 || tfmInformationParts.Length > 2)
                {
                    argumentResult.AddError(invalidPackageAssemblyReferenceFormatMessage);
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
