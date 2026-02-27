// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using static Microsoft.Build.CommandLine.Experimental.CommandLineSwitches;
using static Microsoft.Build.Execution.BuildManager;

#nullable disable

namespace Microsoft.Build.CommandLine.Experimental
{
    internal class CommandLineParser
    {
        /// <summary>
        /// String replacement pattern to support paths in response files.
        /// </summary>
        private const string responseFilePathReplacement = "%MSBuildThisFileDirectory%";

        /// <summary>
        /// The name of an auto-response file to search for in the project directory and above.
        /// </summary>
        private const string directoryResponseFileName = "Directory.Build.rsp";

        /// <summary>
        /// The name of the auto-response file.
        /// </summary>
        private const string autoResponseFileName = "MSBuild.rsp";

        /// <summary>
        /// Used to keep track of response files to prevent them from
        /// being included multiple times (or even recursively).
        /// </summary>
        private List<string> includedResponseFiles;

        internal IReadOnlyList<string> IncludedResponseFiles => includedResponseFiles ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Parses the provided command-line arguments into a <see cref="CommandLineSwitchesAccessor"/>.
        /// </summary>
        /// <param name="commandLineArgs">
        /// The command-line arguments excluding the executable path.
        /// </param>
        /// <returns>
        /// A <see cref="CommandLineSwitchesAccessor"/> containing the effective set of switches after combining
        /// switches from response files (including any auto-response file) with switches from the command line,
        /// where command-line switches take precedence.
        /// </returns>
        /// <exception cref="CommandLineSwitchException">
        /// Thrown when invalid switch syntax or values are encountered while parsing the command line or response files.
        /// </exception>
        public CommandLineSwitchesAccessor Parse(IEnumerable<string> commandLineArgs)
        {
            List<string> args = [BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, ..commandLineArgs];
            List<DeferredBuildMessage> deferredBuildMessages = [];

            GatherAllSwitches(
                args,
                deferredBuildMessages,
                out CommandLineSwitches responseFileSwitches,
                out CommandLineSwitches commandLineSwitches,
                out string fullCommandLine,
                out _);

            CommandLineSwitches result = new();
            result.Append(responseFileSwitches, fullCommandLine); // lowest precedence
            result.Append(commandLineSwitches, fullCommandLine);

            result.ThrowErrors();

            return new CommandLineSwitchesAccessor(result);
        }

        /// <summary>
        /// Gets all specified switches, from the command line, as well as all
        /// response files, including the auto-response file.
        /// </summary>
        /// <param name="commandLine"></param>
        /// <param name="deferredBuildMessages"></param>
        /// <param name="switchesFromAutoResponseFile"></param>
        /// <param name="switchesNotFromAutoResponseFile"></param>
        /// <param name="fullCommandLine"></param>
        /// <returns>Combined bag of switches.</returns>
        internal void GatherAllSwitches(
            IEnumerable<string> commandLineArgs,
            List<DeferredBuildMessage> deferredBuildMessages,
            out CommandLineSwitches switchesFromAutoResponseFile,
            out CommandLineSwitches switchesNotFromAutoResponseFile,
            out string fullCommandLine,
            out string exeName)
        {
            ResetGatheringSwitchesState();

            // discard the first piece, because that's the path to the executable -- the rest are args
            commandLineArgs = commandLineArgs.Skip(1);
            exeName = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

            fullCommandLine = $"'{string.Join(" ", commandLineArgs)}'";

            // parse the command line, and flag syntax errors and obvious switch errors
            switchesNotFromAutoResponseFile = new CommandLineSwitches();
            GatherCommandLineSwitches(commandLineArgs, switchesNotFromAutoResponseFile, fullCommandLine);

            // parse the auto-response file (if "/noautoresponse" is not specified), and combine those switches with the
            // switches on the command line
            switchesFromAutoResponseFile = new CommandLineSwitches();
            if (!switchesNotFromAutoResponseFile[ParameterlessSwitch.NoAutoResponse])
            {
                string exePath = Path.GetDirectoryName(FileUtilities.ExecutingAssemblyPath); // Copied from XMake
                GatherAutoResponseFileSwitches(exePath, switchesFromAutoResponseFile, fullCommandLine);
            }

            CommandLineSwitches switchesFromEnvironmentVariable = new();
            GatherLoggingArgsEnvironmentVariableSwitches(ref switchesFromEnvironmentVariable, deferredBuildMessages, fullCommandLine);
            switchesNotFromAutoResponseFile.Append(switchesFromEnvironmentVariable, fullCommandLine);
        }

        /// <summary>
        /// Gathers and validates logging switches from the MSBUILD_LOGGING_ARGS environment variable.
        /// Only -bl and -check switches are allowed. All other switches are logged as warnings and ignored.
        /// </summary>
        internal void GatherLoggingArgsEnvironmentVariableSwitches(
            ref CommandLineSwitches switches,
            List<DeferredBuildMessage> deferredBuildMessages,
            string commandLine)
        {
            if (string.IsNullOrWhiteSpace(Traits.MSBuildLoggingArgs))
            {
                return;
            }

            DeferredBuildMessageSeverity messageSeverity = Traits.Instance.EmitLogsAsMessage ? DeferredBuildMessageSeverity.Message : DeferredBuildMessageSeverity.Warning;

            try
            {
                List<string> envVarArgs = QuotingUtilities.SplitUnquoted(Traits.MSBuildLoggingArgs);

                List<string> validArgs = new(envVarArgs.Count);
                List<string> invalidArgs = null;

                foreach (string arg in envVarArgs)
                {
                    string unquotedArg = QuotingUtilities.Unquote(arg);
                    if (string.IsNullOrWhiteSpace(unquotedArg))
                    {
                        continue;
                    }

                    if (IsAllowedLoggingArg(unquotedArg))
                    {
                        validArgs.Add(arg);
                    }
                    else
                    {
                        invalidArgs ??= [];
                        invalidArgs.Add(unquotedArg);
                    }
                }

                if (invalidArgs != null)
                {
                    foreach (string invalidArg in invalidArgs)
                    {
                        var message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string warningCode, out _, "LoggingArgsEnvVarUnsupportedArgument", invalidArg);
                        deferredBuildMessages.Add(new DeferredBuildMessage(message, warningCode, messageSeverity));
                    }
                }

                if (validArgs.Count > 0)
                {
                    deferredBuildMessages.Add(new DeferredBuildMessage(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("LoggingArgsEnvVarUsing", string.Join(" ", validArgs)), MessageImportance.Low));
                    GatherCommandLineSwitches(validArgs, switches, commandLine);
                }
            }
            catch (Exception ex)
            {
                var message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "LoggingArgsEnvVarError", ex.ToString());
                deferredBuildMessages.Add(new DeferredBuildMessage(message, errorCode, messageSeverity));
            }
        }

        /// <summary>
        /// Checks if the argument is an allowed logging argument (-bl or -check).
        /// </summary>
        /// <param name="arg">The unquoted argument to check.</param>
        /// <returns>True if the argument is allowed, false otherwise.</returns>
        private bool IsAllowedLoggingArg(string arg)
        {
            if (!ValidateSwitchIndicatorInUnquotedArgument(arg))
            {
                return false;
            }

            ReadOnlySpan<char> switchPart = arg.AsSpan(GetLengthOfSwitchIndicator(arg));

            // Extract switch name (before any ':' parameter indicator)
            int colonIndex = switchPart.IndexOf(':');
            ReadOnlySpan<char> switchNameSpan = colonIndex >= 0 ? switchPart.Slice(0, colonIndex) : switchPart;
            string switchName = switchNameSpan.ToString();

            return IsParameterizedSwitch(
                    switchName,
                    out ParameterizedSwitch paramSwitch,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _) && (paramSwitch == ParameterizedSwitch.BinaryLogger || paramSwitch == ParameterizedSwitch.Check);
        }

        /// <summary>
        /// Coordinates the parsing of the command line. It detects switches on the command line, gathers their parameters, and
        /// flags syntax errors, and other obvious switch errors.
        /// </summary>
        /// <remarks>
        /// Internal for unit testing only.
        /// </remarks>
        internal void GatherCommandLineSwitches(IEnumerable<string> commandLineArgs, CommandLineSwitches commandLineSwitches, string commandLine = "")
        {
            foreach (string commandLineArg in commandLineArgs)
            {
                string unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out var doubleQuotesRemovedFromArg);

                if (unquotedCommandLineArg.Length > 0)
                {
                    // response file switch starts with @
                    if (unquotedCommandLineArg.StartsWith("@", StringComparison.Ordinal))
                    {
                        GatherResponseFileSwitch(unquotedCommandLineArg, commandLineSwitches, commandLine);
                    }
                    else
                    {
                        string switchName;
                        string switchParameters;

                        // all switches should start with - or / or -- unless a project is being specified
                        if (!ValidateSwitchIndicatorInUnquotedArgument(unquotedCommandLineArg) || FileUtilities.LooksLikeUnixFilePath(unquotedCommandLineArg))
                        {
                            switchName = null;
                            // add a (fake) parameter indicator for later parsing
                            switchParameters = $":{commandLineArg}";
                        }
                        else
                        {
                            // check if switch has parameters (look for the : parameter indicator)
                            int switchParameterIndicator = unquotedCommandLineArg.IndexOf(':');

                            // get the length of the beginning sequence considered as a switch indicator (- or / or --)
                            int switchIndicatorsLength = GetLengthOfSwitchIndicator(unquotedCommandLineArg);

                            // extract the switch name and parameters -- the name is sandwiched between the switch indicator (the
                            // leading - or / or --) and the parameter indicator (if the switch has parameters); the parameters (if any)
                            // follow the parameter indicator
                            if (switchParameterIndicator == -1)
                            {
                                switchName = unquotedCommandLineArg.Substring(switchIndicatorsLength);
                                switchParameters = string.Empty;
                            }
                            else
                            {
                                switchName = unquotedCommandLineArg.Substring(switchIndicatorsLength, switchParameterIndicator - switchIndicatorsLength);
                                switchParameters = ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, switchName, switchParameterIndicator, switchIndicatorsLength);
                            }
                        }

                        // Special case: for the switches "/m" (or "/maxCpuCount") and "/bl" (or "/binarylogger") we wish to pretend we saw a default argument
                        // This allows a subsequent /m:n on the command line to override it.
                        // We could create a new kind of switch with optional parameters, but it's a great deal of churn for this single case.
                        // Note that if no "/m" or "/maxCpuCount" switch -- either with or without parameters -- is present, then we still default to 1 cpu
                        // for backwards compatibility.
                        if (string.IsNullOrEmpty(switchParameters))
                        {
                            if (string.Equals(switchName, "m", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(switchName, "maxcpucount", StringComparison.OrdinalIgnoreCase))
                            {
                                int numberOfCpus = NativeMethodsShared.GetLogicalCoreCount();
                                switchParameters = $":{numberOfCpus}";
                            }
                            else if (string.Equals(switchName, "bl", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(switchName, "binarylogger", StringComparison.OrdinalIgnoreCase))
                            {
                                // we have to specify at least one parameter otherwise it's impossible to distinguish the situation
                                // where /bl is not specified at all vs. where /bl is specified without the file name.
                                switchParameters = ":msbuild.binlog";
                            }
                            else if (string.Equals(switchName, "prof", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(switchName, "profileevaluation", StringComparison.OrdinalIgnoreCase))
                            {
                                switchParameters = ":no-file";
                            }
                        }

                        if (CommandLineSwitches.IsParameterlessSwitch(switchName, out var parameterlessSwitch, out var duplicateSwitchErrorMessage))
                        {
                            GatherParameterlessCommandLineSwitch(commandLineSwitches, parameterlessSwitch, switchParameters, duplicateSwitchErrorMessage, unquotedCommandLineArg, commandLine);
                        }
                        else if (CommandLineSwitches.IsParameterizedSwitch(switchName, out var parameterizedSwitch, out duplicateSwitchErrorMessage, out var multipleParametersAllowed, out var missingParametersErrorMessage, out var unquoteParameters, out var allowEmptyParameters))
                        {
                            GatherParameterizedCommandLineSwitch(commandLineSwitches, parameterizedSwitch, switchParameters, duplicateSwitchErrorMessage, multipleParametersAllowed, missingParametersErrorMessage, unquoteParameters, unquotedCommandLineArg, allowEmptyParameters, commandLine);
                        }
                        else
                        {
                            commandLineSwitches.SetUnknownSwitchError(unquotedCommandLineArg, commandLine);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called when a response file switch is detected on the command line. It loads the specified response file, and parses
        /// each line in it like a command line. It also prevents multiple (or recursive) inclusions of the same response file.
        /// </summary>
        /// <param name="unquotedCommandLineArg"></param>
        /// <param name="commandLineSwitches"></param>
        private void GatherResponseFileSwitch(string unquotedCommandLineArg, CommandLineSwitches commandLineSwitches, string commandLine)
        {
            try
            {
                string responseFile = FrameworkFileUtilities.FixFilePath(unquotedCommandLineArg.Substring(1));

                if (responseFile.Length == 0)
                {
                    commandLineSwitches.SetSwitchError("MissingResponseFileError", unquotedCommandLineArg, commandLine);
                }
                else if (!FileSystems.Default.FileExists(responseFile))
                {
                    commandLineSwitches.SetParameterError("ResponseFileNotFoundError", unquotedCommandLineArg, commandLine);
                }
                else
                {
                    // normalize the response file path to help catch multiple (or recursive) inclusions
                    responseFile = Path.GetFullPath(responseFile);
                    // NOTE: for network paths or mapped paths, normalization is not guaranteed to work

                    bool isRepeatedResponseFile = false;

                    foreach (string includedResponseFile in includedResponseFiles)
                    {
                        if (string.Equals(responseFile, includedResponseFile, StringComparison.OrdinalIgnoreCase))
                        {
                            commandLineSwitches.SetParameterError("RepeatedResponseFileError", unquotedCommandLineArg, commandLine);
                            isRepeatedResponseFile = true;
                            break;
                        }
                    }

                    if (!isRepeatedResponseFile)
                    {
                        var responseFileDirectory = FrameworkFileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(responseFile));
                        includedResponseFiles.Add(responseFile);

                        List<string> argsFromResponseFile;

#if FEATURE_ENCODING_DEFAULT
                        using (StreamReader responseFileContents = new StreamReader(responseFile, Encoding.Default)) // HIGHCHAR: If response files have no byte-order marks, then assume ANSI rather than ASCII.
#else
                        using (StreamReader responseFileContents = FileUtilities.OpenRead(responseFile)) // HIGHCHAR: If response files have no byte-order marks, then assume ANSI rather than ASCII.
#endif
                        {
                            argsFromResponseFile = new List<string>();

                            while (responseFileContents.Peek() != -1)
                            {
                                // ignore leading whitespace on each line
                                string responseFileLine = responseFileContents.ReadLine().TrimStart();

                                // skip comment lines beginning with #
                                if (!responseFileLine.StartsWith("#", StringComparison.Ordinal))
                                {
                                    // Allow special case to support a path relative to the .rsp file being processed.
                                    responseFileLine = Regex.Replace(responseFileLine, responseFilePathReplacement,
                                        responseFileDirectory, RegexOptions.IgnoreCase);

                                    // treat each line of the response file like a command line i.e. args separated by whitespace
                                    argsFromResponseFile.AddRange(QuotingUtilities.SplitUnquoted(Environment.ExpandEnvironmentVariables(responseFileLine)));
                                }
                            }
                        }

                        CommandLineSwitches.SwitchesFromResponseFiles.Add((responseFile, string.Join(" ", argsFromResponseFile)));

                        GatherCommandLineSwitches(argsFromResponseFile, commandLineSwitches, commandLine);
                    }
                }
            }
            catch (NotSupportedException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e, commandLine);
            }
            catch (SecurityException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e, commandLine);
            }
            catch (UnauthorizedAccessException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e, commandLine);
            }
            catch (IOException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e, commandLine);
            }
        }

        /// <summary>
        /// Called when a switch that doesn't take parameters is detected on the command line.
        /// </summary>
        /// <param name="commandLineSwitches"></param>
        /// <param name="parameterlessSwitch"></param>
        /// <param name="switchParameters"></param>
        /// <param name="duplicateSwitchErrorMessage"></param>
        /// <param name="unquotedCommandLineArg"></param>
        private static void GatherParameterlessCommandLineSwitch(
            CommandLineSwitches commandLineSwitches,
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch,
            string switchParameters,
            string duplicateSwitchErrorMessage,
            string unquotedCommandLineArg,
            string commandLine)
        {
            // switch should not have any parameters
            if (switchParameters.Length == 0)
            {
                // check if switch is duplicated, and if that's allowed
                if (!commandLineSwitches.IsParameterlessSwitchSet(parameterlessSwitch) ||
                    (duplicateSwitchErrorMessage == null))
                {
                    commandLineSwitches.SetParameterlessSwitch(parameterlessSwitch, unquotedCommandLineArg);
                }
                else
                {
                    commandLineSwitches.SetSwitchError(duplicateSwitchErrorMessage, unquotedCommandLineArg, commandLine);
                }
            }
            else
            {
                commandLineSwitches.SetUnexpectedParametersError(unquotedCommandLineArg, commandLine);
            }
        }

        /// <summary>
        /// Called when a switch that takes parameters is detected on the command line. This method flags errors and stores the
        /// switch parameters.
        /// </summary>
        /// <param name="commandLineSwitches"></param>
        /// <param name="parameterizedSwitch"></param>
        /// <param name="switchParameters"></param>
        /// <param name="duplicateSwitchErrorMessage"></param>
        /// <param name="multipleParametersAllowed"></param>
        /// <param name="missingParametersErrorMessage"></param>
        /// <param name="unquoteParameters"></param>
        /// <param name="unquotedCommandLineArg"></param>
        private static void GatherParameterizedCommandLineSwitch(
            CommandLineSwitches commandLineSwitches,
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
            string switchParameters,
            string duplicateSwitchErrorMessage,
            bool multipleParametersAllowed,
            string missingParametersErrorMessage,
            bool unquoteParameters,
            string unquotedCommandLineArg,
            bool allowEmptyParameters,
            string commandLine)
        {
            if (// switch must have parameters
                (switchParameters.Length > 1) ||
                // unless the parameters are optional
                (missingParametersErrorMessage == null))
            {
                // skip the parameter indicator (if any)
                if (switchParameters.Length > 0)
                {
                    switchParameters = switchParameters.Substring(1);
                }

                if (parameterizedSwitch == CommandLineSwitches.ParameterizedSwitch.Project && IsEnvironmentVariable(switchParameters))
                {
                    commandLineSwitches.SetSwitchError("EnvironmentVariableAsSwitch", unquotedCommandLineArg, commandLine);
                }

                // check if switch is duplicated, and if that's allowed
                if (!commandLineSwitches.IsParameterizedSwitchSet(parameterizedSwitch) ||
                    (duplicateSwitchErrorMessage == null))
                {
                    // save the parameters after unquoting and splitting them if necessary
                    if (!commandLineSwitches.SetParameterizedSwitch(parameterizedSwitch, unquotedCommandLineArg, switchParameters, multipleParametersAllowed, unquoteParameters, allowEmptyParameters))
                    {
                        // if parsing revealed there were no real parameters, flag an error, unless the parameters are optional
                        if (missingParametersErrorMessage != null)
                        {
                            commandLineSwitches.SetSwitchError(missingParametersErrorMessage, unquotedCommandLineArg, commandLine);
                        }
                    }
                }
                else
                {
                    commandLineSwitches.SetSwitchError(duplicateSwitchErrorMessage, unquotedCommandLineArg, commandLine);
                }
            }
            else
            {
                commandLineSwitches.SetSwitchError(missingParametersErrorMessage, unquotedCommandLineArg, commandLine);
            }
        }

        /// <summary>
        /// Identifies if there is rsp files near the project file
        /// </summary>
        /// <returns>true if there autoresponse file was found</returns>
        internal bool CheckAndGatherProjectAutoResponseFile(CommandLineSwitches switchesFromAutoResponseFile, CommandLineSwitches commandLineSwitches, bool recursing, string commandLine)
        {
            bool found = false;

            var projectDirectory = GetProjectDirectory(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Project]);

            if (!recursing && !commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.NoAutoResponse])
            {
                // gather any switches from the first Directory.Build.rsp found in the project directory or above
                string directoryResponseFile = FileUtilities.GetPathOfFileAbove(directoryResponseFileName, projectDirectory);

                found = !string.IsNullOrWhiteSpace(directoryResponseFile) && GatherAutoResponseFileSwitchesFromFullPath(directoryResponseFile, switchesFromAutoResponseFile, commandLine);

                // Don't look for more response files if it's only in the same place we already looked (next to the exe)
                string exePath = Path.GetDirectoryName(FileUtilities.ExecutingAssemblyPath); // Copied from XMake
                if (!string.Equals(projectDirectory, exePath, StringComparison.OrdinalIgnoreCase))
                {
                    // this combines any found, with higher precedence, with the switches from the original auto response file switches
                    found |= GatherAutoResponseFileSwitches(projectDirectory, switchesFromAutoResponseFile, commandLine);
                }
            }

            return found;
        }

        private static string GetProjectDirectory(string[] projectSwitchParameters)
        {
            string projectDirectory = ".";
            ErrorUtilities.VerifyThrow(projectSwitchParameters.Length <= 1, "Expect exactly one project at a time.");

            if (projectSwitchParameters.Length == 1)
            {
                var projectFile = FrameworkFileUtilities.FixFilePath(projectSwitchParameters[0]);

                if (FileSystems.Default.DirectoryExists(projectFile))
                {
                    // the provided argument value is actually the directory
                    projectDirectory = projectFile;
                }
                else
                {
                    InitializationException.VerifyThrow(FileSystems.Default.FileExists(projectFile), "ProjectNotFoundError", projectFile);
                    projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFile));
                }
            }

            return projectDirectory;
        }

        /// <summary>
        /// Extracts a switch's parameters after processing all quoting around the switch.
        /// </summary>
        /// <remarks>
        /// This method is marked "internal" for unit-testing purposes only -- ideally it should be "private".
        /// </remarks>
        /// <param name="commandLineArg"></param>
        /// <param name="unquotedCommandLineArg"></param>
        /// <param name="doubleQuotesRemovedFromArg"></param>
        /// <param name="switchName"></param>
        /// <param name="switchParameterIndicator"></param>
        /// <param name="switchIndicatorsLength"></param>
        /// <returns>The given switch's parameters (with interesting quoting preserved).</returns>
        internal static string ExtractSwitchParameters(
            string commandLineArg,
            string unquotedCommandLineArg,
            int doubleQuotesRemovedFromArg,
            string switchName,
            int switchParameterIndicator,
            int switchIndicatorsLength)
        {

            // find the parameter indicator again using the quoted arg
            // NOTE: since the parameter indicator cannot be part of a switch name, quoting around it is not relevant, because a
            // parameter indicator cannot be escaped or made into a literal
            int quotedSwitchParameterIndicator = commandLineArg.IndexOf(':');

            // check if there is any quoting in the name portion of the switch
            string unquotedSwitchIndicatorAndName = QuotingUtilities.Unquote(commandLineArg.Substring(0, quotedSwitchParameterIndicator), out var doubleQuotesRemovedFromSwitchIndicatorAndName);

            ErrorUtilities.VerifyThrow(switchName == unquotedSwitchIndicatorAndName.Substring(switchIndicatorsLength),
                "The switch name extracted from either the partially or completely unquoted arg should be the same.");

            ErrorUtilities.VerifyThrow(doubleQuotesRemovedFromArg >= doubleQuotesRemovedFromSwitchIndicatorAndName,
                "The name portion of the switch cannot contain more quoting than the arg itself.");

            string switchParameters;
            // if quoting in the name portion of the switch was terminated
            if ((doubleQuotesRemovedFromSwitchIndicatorAndName % 2) == 0)
            {
                // get the parameters exactly as specified on the command line i.e. including quoting
                switchParameters = commandLineArg.Substring(quotedSwitchParameterIndicator);
            }
            else
            {
                // if quoting was not terminated in the name portion of the switch, and the terminal double-quote (if any)
                // terminates the switch parameters
                int terminalDoubleQuote = commandLineArg.IndexOf('"', quotedSwitchParameterIndicator + 1);
                if (((doubleQuotesRemovedFromArg - doubleQuotesRemovedFromSwitchIndicatorAndName) <= 1) &&
                    ((terminalDoubleQuote == -1) || (terminalDoubleQuote == (commandLineArg.Length - 1))))
                {
                    // then the parameters are not quoted in any interesting way, so use the unquoted parameters
                    switchParameters = unquotedCommandLineArg.Substring(switchParameterIndicator);
                }
                else
                {
                    // otherwise, use the quoted parameters, after compensating for the quoting that was started in the name
                    // portion of the switch
                    switchParameters = $":\"{commandLineArg.Substring(quotedSwitchParameterIndicator + 1)}";
                }
            }

            ErrorUtilities.VerifyThrow(switchParameters != null, "We must be able to extract the switch parameters.");

            return switchParameters;
        }

        /// <summary>
        /// Checks whether envVar is an environment variable. MSBuild uses
        /// Environment.ExpandEnvironmentVariables(string), which only
        /// considers %-delimited variables.
        /// </summary>
        /// <param name="envVar">A possible environment variable</param>
        /// <returns>Whether envVar is an environment variable</returns>
        private static bool IsEnvironmentVariable(string envVar)
        {
            return envVar.StartsWith("%") && envVar.EndsWith("%") && envVar.Length > 1;
        }

        /// <summary>
        /// Parses the auto-response file (assumes the "/noautoresponse" switch is not specified on the command line), and combines the
        /// switches from the auto-response file with the switches passed in.
        /// Returns true if the response file was found.
        /// </summary>
        private bool GatherAutoResponseFileSwitches(string path, CommandLineSwitches switchesFromAutoResponseFile, string commandLine)
        {
            string autoResponseFile = Path.Combine(path, autoResponseFileName);
            return GatherAutoResponseFileSwitchesFromFullPath(autoResponseFile, switchesFromAutoResponseFile, commandLine);
        }

        private bool GatherAutoResponseFileSwitchesFromFullPath(string autoResponseFile, CommandLineSwitches switchesFromAutoResponseFile, string commandLine)
        {
            bool found = false;

            // if the auto-response file does not exist, only use the switches on the command line
            if (FileSystems.Default.FileExists(autoResponseFile))
            {
                found = true;
                GatherResponseFileSwitch($"@{autoResponseFile}", switchesFromAutoResponseFile, commandLine);

                // if the "/noautoresponse" switch was set in the auto-response file, flag an error
                if (switchesFromAutoResponseFile[CommandLineSwitches.ParameterlessSwitch.NoAutoResponse])
                {
                    switchesFromAutoResponseFile.SetSwitchError("CannotAutoDisableAutoResponseFile",
                        switchesFromAutoResponseFile.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse), commandLine);
                }

                // Throw errors found in the response file
                switchesFromAutoResponseFile.ThrowErrors();
            }

            return found;
        }

        /// <summary>
        /// Checks whether an argument given as a parameter starts with valid indicator,
        /// <br/>which means, whether switch begins with one of: "/", "-", "--"
        /// </summary>
        /// <param name="unquotedCommandLineArgument">Command line argument with beginning indicator (e.g. --help).
        /// <br/>This argument has to be unquoted, otherwise the first character will always be a quote character "</param>
        /// <returns>true if argument's beginning matches one of possible indicators
        /// <br/>false if argument's beginning doesn't match any of correct indicator
        /// </returns>
        private static bool ValidateSwitchIndicatorInUnquotedArgument(string unquotedCommandLineArgument)
        {
            return unquotedCommandLineArgument.StartsWith("-", StringComparison.Ordinal) // superset of "--"
                || unquotedCommandLineArgument.StartsWith("/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the length of the switch indicator (- or / or --)
        /// <br/>The length returned from this method is deduced from the beginning sequence of unquoted argument.
        /// <br/>This way it will "assume" that there's no further error (e.g. //  or ---) which would also be considered as a correct indicator.
        /// </summary>
        /// <param name="unquotedSwitch">Unquoted argument with leading indicator and name</param>
        /// <returns>Correct length of used indicator
        /// <br/>0 if no leading sequence recognized as correct indicator</returns>
        /// Internal for testing purposes
        internal static int GetLengthOfSwitchIndicator(string unquotedSwitch)
        {
            if (unquotedSwitch.StartsWith("--", StringComparison.Ordinal))
            {
                return 2;
            }
            else if (unquotedSwitch.StartsWith("-", StringComparison.Ordinal) || unquotedSwitch.StartsWith("/", StringComparison.Ordinal))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public void ResetGatheringSwitchesState()
        {
            includedResponseFiles = new List<string>();
            CommandLineSwitches.SwitchesFromResponseFiles = new();
        }
    }
}
