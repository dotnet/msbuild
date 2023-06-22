// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.New;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.TemplateEngine.Abstractions.Components;
using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;
using Microsoft.TemplateEngine.MSBuildEvaluation;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using System.IO;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.Commands;
using Command = System.CommandLine.Command;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.Tools;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-new";
        public const string CommandName = "new";
        private const string EnableProjectContextEvaluationEnvVarName = "DOTNET_CLI_DISABLE_PROJECT_EVAL";
        private const string PrefferedLangEnvVarName = "DOTNET_NEW_PREFERRED_LANG";

        private const string HostIdentifier = "dotnetcli";

        private const VerbosityOptions DefaultVerbosity = VerbosityOptions.normal;

        private static readonly Option<bool> s_disableSdkTemplatesOption = new Option<bool>(
            "--debug:disable-sdk-templates",
            () => false,
            LocalizableStrings.DisableSdkTemplates_OptionDescription).Hide();

        private static readonly Option<bool> s_disableProjectContextEvaluationOption = new Option<bool>(
            "--debug:disable-project-context",
            () => false,
            LocalizableStrings.DisableProjectContextEval_OptionDescription).Hide();

        private static readonly Option<VerbosityOptions> s_verbosityOption = new(
            new string[] { "-v", "--verbosity" },
            () => DefaultVerbosity,
            LocalizableStrings.Verbosity_OptionDescription)
        {
            ArgumentHelpName = CommonLocalizableStrings.LevelArgumentName
        };

        private static readonly Option<bool> s_diagnosticOption =
            CommonOptionsFactory
                .CreateDiagnosticsOption()
                .WithDescription(LocalizableStrings.Diagnostics_OptionDescription);

        internal static readonly Command s_command = GetCommand();

        public static Command GetCommand()
        {
            Command command = NewCommandFactory.Create(CommandName, (Func<ParseResult, CliTemplateEngineHost>)GetEngineHost);
            command.AddGlobalOption(s_disableSdkTemplatesOption);
            command.AddGlobalOption(s_disableProjectContextEvaluationOption);
            command.AddGlobalOption(s_verbosityOption);
            command.AddGlobalOption(s_diagnosticOption);
            return command;

            static CliTemplateEngineHost GetEngineHost(ParseResult parseResult)
            {
                bool disableSdkTemplates = parseResult.GetValue(s_disableSdkTemplatesOption);
                bool disableProjectContext = parseResult.GetValue(s_disableProjectContextEvaluationOption)
                    || Env.GetEnvironmentVariableAsBool(EnableProjectContextEvaluationEnvVarName);
                bool diagnosticMode = parseResult.GetValue(s_diagnosticOption);
                FileInfo? projectPath = parseResult.GetValue(SharedOptions.ProjectPathOption);
                FileInfo? outputPath = parseResult.GetValue(SharedOptions.OutputOption);

                OptionResult? verbosityOptionResult = parseResult.FindResultFor(s_verbosityOption);
                VerbosityOptions verbosity = DefaultVerbosity;

                if (diagnosticMode || CommandLoggingContext.IsVerbose)
                {
                    CommandLoggingContext.SetError(true);
                    CommandLoggingContext.SetOutput(true);
                    CommandLoggingContext.SetVerbose(true);
                    verbosity = VerbosityOptions.diagnostic;
                }
                else if (verbosityOptionResult != null
                    && !verbosityOptionResult.IsImplicit
                    // if verbosityOptionResult contains an error, ArgumentConverter.GetValueOrDefault throws an exception
                    // and callstack is pushed to process output 
                    && string.IsNullOrWhiteSpace(verbosityOptionResult.ErrorMessage))
                {
                    VerbosityOptions userSetVerbosity = verbosityOptionResult.GetValueOrDefault<VerbosityOptions>();
                    if (userSetVerbosity.IsQuiet())
                    {
                        CommandLoggingContext.SetError(false);
                        CommandLoggingContext.SetOutput(false);
                        CommandLoggingContext.SetVerbose(false);
                    }
                    else if (userSetVerbosity.IsMinimal())
                    {
                        CommandLoggingContext.SetError(true);
                        CommandLoggingContext.SetOutput(false);
                        CommandLoggingContext.SetVerbose(false);
                    }
                    else if (userSetVerbosity.IsNormal())
                    {
                        CommandLoggingContext.SetError(true);
                        CommandLoggingContext.SetOutput(true);
                        CommandLoggingContext.SetVerbose(false);
                    }
                    verbosity = userSetVerbosity;
                }
                Reporter.Reset();
                return CreateHost(disableSdkTemplates, disableProjectContext, projectPath, outputPath, parseResult, verbosity.ToLogLevel());
            }
        }

        private static CliTemplateEngineHost CreateHost(
            bool disableSdkTemplates,
            bool disableProjectContext,
            FileInfo? projectPath,
            FileInfo? outputPath,
            ParseResult parseResult,
            LogLevel logLevel)
        {
            var builtIns = new List<(Type InterfaceType, IIdentifiedComponent Instance)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Cli.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateSearch.Common.Components.AllComponents);

            //post actions
            builtIns.AddRange(new (Type, IIdentifiedComponent)[]
            {
                (typeof(IPostActionProcessor), new DotnetAddPostActionProcessor()),
                (typeof(IPostActionProcessor), new DotnetSlnPostActionProcessor()),
                (typeof(IPostActionProcessor), new DotnetRestorePostActionProcessor())
            });
            if (!disableSdkTemplates)
            {
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackageProviderFactory()));
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new OptionalWorkloadProviderFactory()));
            }
            if (!disableProjectContext)
            {
                builtIns.Add((typeof(IBindSymbolSource), new ProjectContextSymbolSource()));
                builtIns.Add((typeof(ITemplateConstraintFactory), new ProjectCapabilityConstraintFactory()));
                builtIns.Add((typeof(MSBuildEvaluator), new MSBuildEvaluator(outputDirectory: outputPath?.FullName, projectPath: projectPath?.FullName)));
            }

            builtIns.Add((typeof(IWorkloadsInfoProvider), new WorkloadsInfoProvider(
                    new Lazy<IWorkloadsRepositoryEnumerator>(() => new WorkloadInfoHelper(parseResult.HasOption(SharedOptions.InteractiveOption)))))
            );
            builtIns.Add((typeof(ISdkInfoProvider), new SdkInfoProvider()));

            string? preferredLangEnvVar = Environment.GetEnvironmentVariable(PrefferedLangEnvVarName);
            string preferredLang = string.IsNullOrWhiteSpace(preferredLangEnvVar)? "C#" : preferredLangEnvVar;

            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", preferredLang },
                { "dotnet-cli-version", Product.Version },
                { "RuntimeFrameworkVersion", new Muxer().SharedFxVersion },
                { "NetStandardImplicitPackageVersion", new FrameworkDependencyFile().GetNetStandardLibraryVersion() },
            };
            return new CliTemplateEngineHost(
                HostIdentifier,
                Product.Version,
                preferences,
                builtIns,
                outputPath: outputPath?.FullName,
                logLevel: logLevel);
        }
    }
}
