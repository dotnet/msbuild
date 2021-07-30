// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Parsing;
using LocalizableStrings = Microsoft.DotNet.Tools.Format.LocalizableStrings;
using Microsoft.DotNet.Tools;
using System.Collections.Generic;
using System;

namespace Microsoft.DotNet.Cli.Format
{
    internal static class FormatCommandCommon
    {
        private static string[] VerbosityLevels => new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };
        private static string[] SeverityLevels => new[] { "info", "warn", "error" };

        public static readonly Argument<string> SlnOrProjectArgument = new Argument<string>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        internal static readonly Option<bool> NoRestoreOption = new(new[] { "--no-restore" }, LocalizableStrings.Doesnt_execute_an_implicit_restore_before_formatting);
        internal static readonly Option<bool> VerifyNoChanges = new(new[] { "--verify-no-changes" }, LocalizableStrings.Verify_no_formatting_changes_would_be_performed_Terminates_with_a_non_zero_exit_code_if_any_files_would_have_been_formatted);
        internal static readonly Option<string[]> DiagnosticsOption = new(new[] { "--diagnostics" }, () => Array.Empty<string>(), LocalizableStrings.A_space_separated_list_of_diagnostic_ids_to_use_as_a_filter_when_fixing_code_style_or_3rd_party_issues);
        internal static readonly Option<string> SeverityOption = new Option<string>("--severity", LocalizableStrings.The_severity_of_diagnostics_to_fix_Allowed_values_are_info_warn_and_error).FromAmong(SeverityLevels);
        internal static readonly Option<string[]> IncludeOption = new(new[] { "--include" }, () => Array.Empty<string>(), LocalizableStrings.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty);
        internal static readonly Option<string[]> ExcludeOption = new(new[] { "--exclude" }, () => Array.Empty<string>(), LocalizableStrings.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting);
        internal static readonly Option<bool> IncludeGeneratedOption = new(new[] { "--include-generated" }, LocalizableStrings.Format_files_generated_by_the_SDK);
        internal static readonly Option<string> VerbosityOption = new Option<string>(new[] { "--verbosity", "-v" }, LocalizableStrings.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic).FromAmong(VerbosityLevels);
        internal static readonly Option BinarylogOption = new Option(new[] { "--binarylog" }, LocalizableStrings.Log_all_project_or_solution_load_information_to_a_binary_log_file, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
        {
            ArgumentHelpName = "binary-log-path"
        }.LegalFilePathsOnly();
        internal static readonly Option ReportOption = new Option(new[] { "--report" }, LocalizableStrings.Accepts_a_file_path_which_if_provided_will_produce_a_json_report_in_the_given_directory, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
        {
            ArgumentHelpName = "report-path"
        }.LegalFilePathsOnly();


        public static void AddCommonOptions(this Command command)
        {
            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(NoRestoreOption);
            command.AddOption(VerifyNoChanges);
            command.AddOption(IncludeOption);
            command.AddOption(ExcludeOption);
            command.AddOption(IncludeGeneratedOption);
            command.AddOption(VerbosityOption);
            command.AddOption(BinarylogOption);
            command.AddOption(ReportOption);
        }

        public static void AddCommonDotnetFormatArgs(this List<string> dotnetFormatArgs, ParseResult parseResult)
        {
            if (parseResult.HasOption(NoRestoreOption))
            {
                dotnetFormatArgs.Add("--no-restore");
            }

            if (parseResult.HasOption(VerifyNoChanges))
            {
                dotnetFormatArgs.Add("--check");
            }

            if (parseResult.HasOption(IncludeGeneratedOption))
            {
                dotnetFormatArgs.Add("--include-generated");
            }

            if (parseResult.HasOption(VerbosityOption) &&
                parseResult.ValueForOption(VerbosityOption) is string { Length: > 0 } verbosity)
            {
                dotnetFormatArgs.Add("--verbosity");
                dotnetFormatArgs.Add(verbosity);
            }

            if (parseResult.HasOption(IncludeOption) &&
                parseResult.ValueForOption(IncludeOption) is string[] { Length: > 0 } fileToInclude)
            {
                dotnetFormatArgs.Add("--include");
                dotnetFormatArgs.Add(string.Join(" ", fileToInclude));
            }

            if (parseResult.HasOption(ExcludeOption) &&
                parseResult.ValueForOption(ExcludeOption) is string[] { Length: > 0 } fileToExclude)
            {
                dotnetFormatArgs.Add("--exclude");
                dotnetFormatArgs.Add(string.Join(" ", fileToExclude));
            }

            if (parseResult.HasOption(ReportOption))
            {
                dotnetFormatArgs.Add("--report");
                if (parseResult.ValueForOption(ReportOption) is string { Length: > 0 } reportPath)
                {
                    dotnetFormatArgs.Add(reportPath);
                }
            }

            if (parseResult.HasOption(BinarylogOption))
            {
                dotnetFormatArgs.Add("--binarylog");
                if (parseResult.ValueForOption(BinarylogOption) is string { Length: > 0 } binaryLogPath)
                {
                    dotnetFormatArgs.Add(binaryLogPath);
                }
            }
        }

        public static void AddFormattingDotnetFormatArgs(this List<string> dotnetFormatArgs, ParseResult parseResult)
        {
            dotnetFormatArgs.Add("--fix-whitespace");
        }

        public static void AddStyleDotnetFormatArgs(this List<string> dotnetFormatArgs, ParseResult parseResult)
        {
            dotnetFormatArgs.Add("--fix-style");
            if (parseResult.HasOption(SeverityOption) && 
                parseResult.ValueForOption(SeverityOption) is string { Length: > 0 } styleSeverity)
            {
                dotnetFormatArgs.Add(styleSeverity);
            }
        }

        public static void AddAnalyzerDotnetFormatArgs(this List<string> dotnetFormatArgs, ParseResult parseResult)
        {
            dotnetFormatArgs.Add("--fix-analyzers");
            if (parseResult.HasOption(SeverityOption) &&
                parseResult.ValueForOption(SeverityOption) is string { Length: > 0 } analyzerSeverity)
            {
                dotnetFormatArgs.Add(analyzerSeverity);
            }

            if (parseResult.HasOption(DiagnosticsOption) &&
                parseResult.ValueForOption(DiagnosticsOption) is string[] { Length: > 0 } diagnostics)
            {
                dotnetFormatArgs.Add("--diagnostics");
                dotnetFormatArgs.Add(string.Join(" ", diagnostics));
            }
        }

        public static void AddProjectOrSolutionDotnetFormatArgs(this List<string> dotnetFormatArgs, ParseResult parseResult)
        {
            if (parseResult.ValueForArgument<string>(SlnOrProjectArgument) is string { Length: > 0 } slnOrProject)
            {
                dotnetFormatArgs.Add(slnOrProject);
            }
        }
    }
}
