// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.Commands;
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

        private static readonly CliOption<bool> s_disableSdkTemplatesOption = new CliOption<bool>("--debug:disable-sdk-templates")
        {
            DefaultValueFactory = static _ => false,
            Description = LocalizableStrings.DisableSdkTemplates_OptionDescription,
            Recursive = true
        }.Hide();

        private static readonly CliOption<bool> s_disableProjectContextEvaluationOption = new CliOption<bool>(
            "--debug:disable-project-context")
        {
            DefaultValueFactory = static _ => false,
            Description = LocalizableStrings.DisableProjectContextEval_OptionDescription,
            Recursive = true
        }.Hide();

        private static readonly CliOption<VerbosityOptions> s_verbosityOption = new("--verbosity", "-v")
        {
            DefaultValueFactory = _ => DefaultVerbosity,
            Description = LocalizableStrings.Verbosity_OptionDescription,
            HelpName = CommonLocalizableStrings.LevelArgumentName,
            Recursive = true
        };

        private static readonly CliOption<bool> s_diagnosticOption =
            CommonOptionsFactory
                .CreateDiagnosticsOption(recursive: true)
                .WithDescription(LocalizableStrings.Diagnostics_OptionDescription);

        internal static readonly CliCommand s_command = GetCommand();

        public static CliCommand GetCommand()
        {
            CliCommand command = NewCommandFactory.Create(CommandName, (Func<ParseResult, CliTemplateEngineHost>)GetEngineHost);
            command.Options.Add(s_disableSdkTemplatesOption);
            command.Options.Add(s_disableProjectContextEvaluationOption);
            command.Options.Add(s_verbosityOption);
            command.Options.Add(s_diagnosticOption);
            return command;

            static CliTemplateEngineHost GetEngineHost(ParseResult parseResult)
            {
                bool disableSdkTemplates = parseResult.GetValue(s_disableSdkTemplatesOption);
                bool disableProjectContext = parseResult.GetValue(s_disableProjectContextEvaluationOption)
                    || Env.GetEnvironmentVariableAsBool(EnableProjectContextEvaluationEnvVarName);
                bool diagnosticMode = parseResult.GetValue(s_diagnosticOption);
                FileInfo? projectPath = parseResult.GetValue(SharedOptions.ProjectPathOption);
                FileInfo? outputPath = parseResult.GetValue(SharedOptions.OutputOption);

                OptionResult? verbosityOptionResult = parseResult.GetResult(s_verbosityOption);
                VerbosityOptions verbosity = DefaultVerbosity;

                if (diagnosticMode || CommandLoggingContext.IsVerbose)
                {
                    CommandLoggingContext.SetError(true);
                    CommandLoggingContext.SetOutput(true);
                    CommandLoggingContext.SetVerbose(true);
                    verbosity = VerbosityOptions.diagnostic;
                }
                else if (verbosityOptionResult != null
                    && !verbosityOptionResult.Implicit
                    // if verbosityOptionResult contains an error, ArgumentConverter.GetValueOrDefault throws an exception
                    // and callstack is pushed to process output 
                    && !parseResult.Errors.Any(error => error.SymbolResult == verbosityOptionResult))
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
