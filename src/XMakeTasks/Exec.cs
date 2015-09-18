// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class defines an "Exec" MSBuild task, which simply invokes the specified process with the specified arguments, waits
    /// for it to complete, and then returns True if the process completed successfully, and False if an error occurred.
    /// </summary>
    /// <comments>
    /// UNDONE: ToolTask has a "UseCommandProcessor" flag that duplicates much of the code in this class. Remove the duplication.
    /// </comments>
    public class Exec : ToolTaskExtension
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Exec()
        {
            Command = string.Empty;

            // Console-based output uses the current system OEM code page by default. Note that we should not use Console.OutputEncoding
            // here since processes we run don't really have much to do with our console window (and also Console.OutputEncoding
            // doesn't return the OEM code page if the running application that hosts MSBuild is not a console application).
            // Note: 8/12/15 - Changed encoding to use UTF8 when OS version is greater than or equal to 6.1 (Windows 7)
            _standardOutputEncoding = GetEncodingWithOsFallback();
            _standardErrorEncoding = GetEncodingWithOsFallback();
        }

        #endregion

        #region Fields

        // Are the ecodings for StdErr and StdOut streams valid
        private bool _encodingParametersValid = true;
        private string _workingDirectory;
        private ITaskItem[] _outputs;
        internal bool workingDirectoryIsUNC; // internal for unit testing
        private string _batchFile;
        private string _customErrorRegex;
        private string _customWarningRegex;
        private bool _ignoreStandardErrorWarningFormat = false; // By default, detect standard-format errors
        private readonly List<ITaskItem> _nonEmptyOutput = new List<ITaskItem>();
        private Encoding _standardErrorEncoding;
        private Encoding _standardOutputEncoding;

        #endregion

        #region Properties

        [Required]
        public string Command { get; set; }

        public string WorkingDirectory { get; set; }

        public bool IgnoreExitCode { get; set; }

        /// <summary>
        /// Enable the pipe of the standard out to an item (StandardOutput).
        /// </summary>
        /// <Remarks>
        /// Even thought this is called a pipe, it is infact a Tee.  Use StandardOutputImportance to adjust the visibility of the stdout.
        /// </Remarks>
        public bool ConsoleToMSBuild { get; set; }

        /// <summary>
        /// Users can supply a regular expression that we should
        /// use to spot error lines in the tool output. This is
        /// useful for tools that produce unusually formatted output
        /// </summary>
        public string CustomErrorRegularExpression
        {
            get { return _customErrorRegex; }
            set { _customErrorRegex = value; }
        }

        /// <summary>
        /// Users can supply a regular expression that we should
        /// use to spot warning lines in the tool output. This is
        /// useful for tools that produce unusually formatted output
        /// </summary>
        public string CustomWarningRegularExpression
        {
            get { return _customWarningRegex; }
            set { _customWarningRegex = value; }
        }

        /// <summary>
        /// Whether to use pick out lines in the output that match
        /// the standard error/warning format, and log them as errors/warnings.
        /// Defaults to true.
        /// </summary>
        public bool IgnoreStandardErrorWarningFormat
        {
            get { return _ignoreStandardErrorWarningFormat; }
            set { _ignoreStandardErrorWarningFormat = value; }
        }

        /// <summary>
        /// Property specifying the encoding of the captured task standard output stream
        /// </summary>
        protected override Encoding StandardOutputEncoding
        {
            get { return _standardOutputEncoding; }
        }

        /// <summary>
        /// Property specifying the encoding of the captured task standard error stream
        /// </summary>
        protected override Encoding StandardErrorEncoding
        {
            get { return _standardErrorEncoding; }
        }

        /// <summary>
        /// Project visible property specifying the encoding of the captured task standard output stream
        /// </summary>
        [Output]
        public string StdOutEncoding
        {
            get { return StandardOutputEncoding.EncodingName; }
            set
            {
                try
                {
                    _standardOutputEncoding = Encoding.GetEncoding(value);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidValue", "StdOutEncoding", "Exec");
                    _encodingParametersValid = false;
                }
            }
        }

        /// <summary>
        /// Project visible property specifying the encoding of the captured task standard error stream
        /// </summary>
        [Output]
        public string StdErrEncoding
        {
            get { return StandardErrorEncoding.EncodingName; }
            set
            {
                try
                {
                    _standardErrorEncoding = Encoding.GetEncoding(value);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidValue", "StdErrEncoding", "Exec");
                    _encodingParametersValid = false;
                }
            }
        }

        [Output]
        public ITaskItem[] Outputs
        {
            get { return _outputs ?? new ITaskItem[0]; }
            set { _outputs = value; }
        }

        /// <summary>
        /// Returns the output as an Item.  Whitespace are trimmed.
        /// ConsoleOutput is enabled when ConsoleToMSBuild is true.  This avoids holding lines in memory
        /// if they aren't used.  ConsoleOutput is a combination of stdout and stderr.
        /// </summary>
        [Output]
        public ITaskItem[] ConsoleOutput
        {
            get { return !ConsoleToMSBuild ? new ITaskItem[0] : _nonEmptyOutput.ToArray(); }
        }

        #endregion

        #region Methods
        /// <summary>
        /// Write out a temporary batch file with the user-specified command in it.
        /// </summary>
        private void CreateTemporaryBatchFile()
        {
            // Temporary file with the extension .Exec.bat
            _batchFile = FileUtilities.GetTemporaryFile(".exec.cmd");

            // UNICODE Batch files are not allowed as of WinXP. We can't use normal ANSI code pages either,
            // since console-related apps use OEM code pages "for historical reasons". Sigh.
            // We need to get the current OEM code page which will be the same language as the current ANSI code page,
            // just the OEM version.
            // See http://www.microsoft.com/globaldev/getWR/steps/wrg_codepage.mspx for a discussion of ANSI vs OEM
            // Note: 8/12/15 - Switched to use UTF8 on OS newer than 6.1 (Windows 7)
            using (StreamWriter sw = new StreamWriter(_batchFile, false, GetEncodingWithOsFallback()))
            {
                // In some wierd setups, users may have set an env var actually called "errorlevel"
                // this would cause our "exit %errorlevel%" to return false.
                // This is because the actual errorlevel value is not an environment variable, but some commands,
                // such as "exit %errorlevel%" will use the environment variable with that name if it exists, instead
                // of the actual errorlevel value. So we must temporarily reset errorlevel locally first.
                sw.WriteLine("setlocal");
                // One more wrinkle.
                // "set foo=" has odd behavior: it sets errorlevel to 1 if there was no environment variable named
                // "foo" defined.
                // This has the effect of making "set errorlevel=" set an errorlevel of 1 if an environment
                // variable named "errorlevel" didn't already exist!
                // To avoid this problem, set errorlevel locally to a dummy value first.
                sw.WriteLine("set errorlevel=dummy");
                sw.WriteLine("set errorlevel=");

                // We probably have to change the code page to UTF8 (65001) for non-ansi characters to work.
                if (EncodingUtilities.CurrentSystemOemEncoding.CodePage != sw.Encoding.CodePage)
                {
                    // Output to nul so we don't change output and logs.
                    sw.WriteLine(string.Format(@"%SystemRoot%\System32\chcp.com {0}>nul", sw.Encoding.CodePage));
                }

                // if the working directory is a UNC path, bracket the exec command with pushd and popd, because pushd
                // automatically maps the network path to a drive letter, and then popd disconnects it
                if (workingDirectoryIsUNC)
                {
                    sw.WriteLine("pushd " + _workingDirectory);
                }

                sw.WriteLine(Command);

                if (workingDirectoryIsUNC)
                {
                    sw.WriteLine("popd");
                }

                // NOTES:
                // 1) there's a bug in the Process class where the exit code is not returned properly i.e. if the command
                //    fails with exit code 9009, Process.ExitCode returns 1 -- the statement below forces it to return the
                //    correct exit code
                // 2) also because of another (or perhaps the same) bug in the Process class, when we use pushd/popd for a
                //    UNC path, even if the command fails, the exit code comes back as 0 (seemingly reflecting the success
                //    of popd) -- the statement below fixes that too
                // 3) the above described behaviour is most likely bugs in the Process class because batch files in a
                //    console window do not hide or change the exit code a.k.a. errorlevel, esp. since the popd command is
                //    a no-fail command, and it never changes the previous errorlevel
                sw.WriteLine("exit %errorlevel%");
            }
        }
        #endregion

        #region Overridden methods

        /// <summary>
        /// Executes cmd.exe and waits for it to complete
        /// </summary>
        /// <remarks>
        /// Overridden to clean up the batch file afterwards.
        /// </remarks>
        /// <returns>Upon completion of the process, returns True if successful, False if not.</returns>
        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            try
            {
                return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
            }
            finally
            {
                DeleteTempFile(_batchFile);
            }
        }

        /// <summary>
        /// Allows tool to handle the return code.
        /// This method will only be called with non-zero exitCode set to true.
        /// </summary>
        /// <remarks>
        /// Overridden to make sure we display the command we put in the batch file, not the cmd.exe command
        /// used to run the batch file.
        /// </remarks>
        protected override bool HandleTaskExecutionErrors()
        {
            if (IgnoreExitCode)
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "Exec.CommandFailedNoErrorCode", this.Command, ExitCode);
                return true;
            }

            if (ExitCode == NativeMethods.SE_ERR_ACCESSDENIED)
            {
                Log.LogErrorWithCodeFromResources("Exec.CommandFailedAccessDenied", this.Command, ExitCode);
            }
            else
            {
                Log.LogErrorWithCodeFromResources("Exec.CommandFailed", this.Command, ExitCode);
            }
            return false;
        }

        /// <summary>
        /// Logs the tool name and the path from where it is being run.
        /// </summary>
        /// <remarks>
        /// Overridden to avoid logging the path to "cmd.exe", which is not interesting.
        /// </remarks>
        protected override void LogPathToTool(string toolName, string pathToTool)
        {
            // Do nothing
        }

        /// <summary>
        /// Logs the command to be executed.
        /// </summary>
        /// <remarks>
        /// Overridden to log the batch file command instead of the cmd.exe command.
        /// </remarks>
        /// <param name="message"></param>
        protected override void LogToolCommand(string message)
        {
            //Dont print the command line if Echo is Off.
            if (!EchoOff)
            {
                base.LogToolCommand(Command);
            }
        }

        /// <summary>
        /// Calls a method on the TaskLoggingHelper to parse a single line of text to
        /// see if there are any errors or warnings in canonical format.
        /// </summary>
        /// <remarks>
        /// Overridden to handle any custom regular expressions supplied.
        /// </remarks>
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            if (OutputMatchesRegex(singleLine, ref _customErrorRegex))
            {
                Log.LogError(singleLine);
            }
            else if (OutputMatchesRegex(singleLine, ref _customWarningRegex))
            {
                Log.LogWarning(singleLine);
            }
            else if (_ignoreStandardErrorWarningFormat)
            {
                // Not detecting regular format errors and warnings, and it didn't
                // match any regexes either -- log as a regular message
                Log.LogMessage(messageImportance, singleLine, null);
            }
            else
            {
                // This is the normal code path: match standard format errors and warnings
                Log.LogMessageFromText(singleLine, messageImportance);
            }

            if (ConsoleToMSBuild)
            {
                string trimmedTextLine = singleLine.Trim();
                if (trimmedTextLine.Length > 0)
                {
                    // The lines read may be unescaped, so we need to escape them
                    // before passing them to the TaskItem.
                    _nonEmptyOutput.Add(new TaskItem(EscapingUtilities.Escape(trimmedTextLine)));
                }
            }
        }

        /// <summary>
        /// Returns true if the string is matched by the regular expression.
        /// If the regular expression is invalid, logs an error, then clears it out to
        /// prevent more errors.
        /// </summary>
        private bool OutputMatchesRegex(string singleLine, ref string regularExpression)
        {
            if (regularExpression == null)
            {
                return false;
            }

            bool match = false;

            try
            {
                match = Regex.IsMatch(singleLine, regularExpression);
            }
            catch (ArgumentException ex)
            {
                Log.LogErrorWithCodeFromResources("Exec.InvalidRegex", regularExpression, ex.Message);
                // Clear out the regex so there won't be any more errors; let the tool continue,
                // then it will fail because of the error we just logged
                regularExpression = null;
            }

            return match;
        }

        /// <summary>
        /// Validate the task arguments, log any warnings/errors
        /// </summary>
        /// <returns>true if arguments are corrent enough to continue processing, false otherwise</returns>
        protected override bool ValidateParameters()
        {
            // If either of the encoding parameters passed to the task were
            // invalid, then we should report that fact back to tooltask
            if (!_encodingParametersValid)
            {
                return false;
            }

            // Make sure that at least the Command property was set
            if (Command.Trim().Length == 0)
            {
                Log.LogErrorWithCodeFromResources("Exec.MissingCommandError");
                return false;
            }

            // determine what the working directory for the exec command is going to be -- if the user specified a working
            // directory use that, otherwise it's the current directory
            _workingDirectory = !string.IsNullOrEmpty(WorkingDirectory)
                ? WorkingDirectory
                : Directory.GetCurrentDirectory();

            // check if the working directory we're going to use for the exec command is a UNC path
            workingDirectoryIsUNC = FileUtilitiesRegex.UNCPattern.IsMatch(_workingDirectory);

            // if the working directory is a UNC path, and all drive letters are mapped, bail out, because the pushd command
            // will not be able to auto-map to the UNC path
            if (workingDirectoryIsUNC && (DriveInfo.GetDrives().Length == 26))
            {
                Log.LogErrorWithCodeFromResources("Exec.AllDriveLettersMappedError", _workingDirectory);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Accessor for ValidateParameters purely for unit-test use
        /// </summary>
        /// <returns></returns>
        internal bool ValidateParametersAccessor()
        {
            return ValidateParameters();
        }

        /// <summary>
        /// Determining the path to cmd.exe
        /// </summary>
        /// <returns>path to cmd.exe</returns>
        protected override string GenerateFullPathToTool()
        {
            // Get the fully qualified path to cmd.exe
            return ToolLocationHelper.GetPathToSystemFile("cmd.exe");
        }

        /// <summary>
        /// Gets the working directory to use for the process. Should return null if ToolTask should use the
        /// current directory.
        /// May throw an IOException if the directory to be used is somehow invalid.
        /// </summary>
        /// <returns>working directory</returns>
        protected override string GetWorkingDirectory()
        {
            // If the working directory is UNC, we're going to use "pushd" in the batch file to set it.
            // If it's invalid, pushd won't fail: it will just go ahead and use the system folder.
            // So verify it's valid here.
            if (!Directory.Exists(_workingDirectory))
            {
                throw new DirectoryNotFoundException(ResourceUtilities.FormatResourceString("Exec.InvalidWorkingDirectory", _workingDirectory));
            }

            if (workingDirectoryIsUNC)
            {
                // if the working directory for the exec command is UNC, set the process working directory to the system path
                // so that it doesn't display this silly error message:
                //      '\\<server>\<share>'
                //      CMD.EXE was started with the above path as the current directory.
                //      UNC paths are not supported.  Defaulting to Windows directory.
                return ToolLocationHelper.PathToSystem;
            }
            else
            {
                return _workingDirectory;
            }
        }

        /// <summary>
        /// Accessor for GetWorkingDirectory purely for unit-test use
        /// </summary>
        /// <returns></returns>
        internal string GetWorkingDirectoryAccessor()
        {
            return GetWorkingDirectory();
        }

        /// <summary>
        /// Adds the arguments for cmd.exe
        /// </summary>
        /// <param name="commandLine">command line builder class to add arguments to</param>
        protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
        {
            // Create the batch file now,
            // so we have the file name for the cmd.exe command line
            CreateTemporaryBatchFile();

            string batchFileForCommandLine = _batchFile;
            commandLine.AppendSwitch("/Q");      // echo off
            commandLine.AppendSwitch("/C");      // run then terminate

            // If for some crazy reason the path has a & character and a space in it
            // then get the short path of the temp path, which should not have spaces in it
            // and then escape the &
            if (batchFileForCommandLine.Contains("&") && !batchFileForCommandLine.Contains("^&"))
            {
                batchFileForCommandLine = NativeMethodsShared.GetShortFilePath(batchFileForCommandLine);
                batchFileForCommandLine = batchFileForCommandLine.Replace("&", "^&");
            }

            commandLine.AppendFileNameIfNotNull(batchFileForCommandLine);
        }

        #endregion

        #region Overridden properties

        /// <summary>
        /// The name of the tool to execute
        /// </summary>
        protected override string ToolName
        {
            get { return "cmd.exe"; }
        }

        /// <summary>
        /// Importance with which to log ordinary messages in the
        /// standard error stream.
        /// </summary>
        protected override MessageImportance StandardErrorLoggingImportance
        {
            get { return MessageImportance.High; }
        }

        /// <summary>
        /// Importance with which to log ordinary messages in the
        /// standard out stream.
        /// </summary>
        /// <remarks>
        /// Overridden to increase from the default "Low" up to "High".
        /// </remarks>
        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; }
        }

        #endregion

        private static readonly Encoding s_utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>
        /// Get encoding based on OS. This will fall back to previous behavior on any OS before Windows 7.
        /// If the OS is greater than or equal to Windows 7, UTF8 encoding will be used for the cmd file.
        /// </summary>
        /// <remarks>UTF8 w/o BOM is used because cmd.exe does not like a BOM in a .cmd file.</remarks>
        /// <returns>Encoding to use</returns>
        private Encoding GetEncodingWithOsFallback()
        {
            // Windows 7 (6.1) or greater
            var windows7 = new Version(6, 1);

            return Environment.OSVersion.Version >= windows7
                ? s_utf8WithoutBom
                : EncodingUtilities.CurrentSystemOemEncoding;
        }
    }
}
