// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// (1) Make sure values containing hyphens are quoted (RC at least requires this)
    /// (2) Escape any embedded quotes. 
    ///     -- Literal double quotes should be written in the form \" not ""
    ///     -- Backslashes falling just before doublequotes must be doubled.
    ///     -- Literal double quotes can only occur in pairs (you cannot pass a single literal double quote)
    /// 	-- Functional double quotes (for example to handle spaces) are best put around both name and value
    /// 	    in switches like /Dname=value.
    /// </summary>
    /// <remarks>
    /// 
    /// Below are some quoting experiments, using the /D switch with the CL and RC preprocessor.
    /// The /D switch is a little more tricky than most switches, because it has a name=value pair.
    /// The table below contains what the preprocessor actually embeds when passed the switch in the
    /// first column:
    /// 
    ///                      CL via cmd line         CL via response file       RC
    ///     /DFOO="A"                A                   A   
    ///     /D"FOO="A""              A                   A                       A
    ///     /DFOO=A                  A                   A   
    ///     /D"FOO=A"                A                   A   
    ///     /DFOO=""A""              A                   A                       A
    ///         
    ///     /DFOO=\"A\"             "A"                                         "A"
    ///     /DFOO="""A"""           "A"                broken                   "A"
    ///     /D"FOO=\"A\""           "A"                                         "A"
    ///     /D"FOO=""A"""           "A"                                         "A"
    ///         
    ///     /DFOO="A B"             A B                 A B 
    ///     /D"FOO=A B"             A B                 A B 
    ///         
    ///     /D"FOO="A B""          broken      
    ///     /DFOO=\"A B\"          broken      
    ///     /D"FOO=\"A B\""        "A B"               "A B"                   "A B"
    ///     /D"FOO=""A B"""        "A B"               broken                  broken
    ///
    /// From my experiments (with CL and RC only) it seems that 
    ///    -- Literal double quotes are most reliably written in the form \" not ""
    ///    -- Backslashes falling just before doublequotes must be doubled.
    ///    -- Values containing literal double quotes must be quoted.
    ///    -- Literal double quotes can only occur in pairs (you cannot pass a single literal double quote)
    ///    -- For /Dname=value style switches, functional double quotes (for example to handle spaces) are best put around both 
    ///           name and value (in other words, these kinds of switches don't need special treatment for their '=' signs).
    ///    -- Values containing hyphens should be quoted; RC requires this, and CL does not mind.
    /// </remarks>
    public class CommandLineBuilder
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public CommandLineBuilder()
        {
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public CommandLineBuilder(bool quoteHyphensOnCommandLine)
        {
            _quoteHyphens = quoteHyphensOnCommandLine;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public CommandLineBuilder(bool quoteHyphensOnCommandLine, bool useNewLineSeparator)
            : this(quoteHyphensOnCommandLine)
        {
            _useNewLineSeparator = useNewLineSeparator;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of the current command
        /// </summary>
        public int Length => CommandLine.Length;

        /// <summary>
        /// Retrieves the private StringBuilder instance for inheriting classes
        /// </summary>
        protected StringBuilder CommandLine { get; } = new StringBuilder();

        #endregion

        #region Basic methods

        /// <summary>
        /// Return the command-line as a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => CommandLine.ToString();


        // Use if escaping of hyphens is supposed to take place
        private static readonly string s_allowedUnquotedRegexNoHyphen =
                         "^"                             // Beginning of line
                       + @"[a-z\\/:0-9\._+=]*"
                       + "$";

        private static readonly string s_definitelyNeedQuotesRegexWithHyphen = @"[|><\s,;\-""]+";

        // Use if escaping of hyphens is not to take place
        private static readonly string s_allowedUnquotedRegexWithHyphen =
                        "^"                             // Beginning of line
                       + @"[a-z\\/:0-9\._\-+=]*"       //  Allow hyphen to be unquoted
                       + "$";
        private static readonly string s_definitelyNeedQuotesRegexNoHyphen = @"[|><\s,;""]+";

        /// <summary>
        ///  Should hyphens be quoted or not
        /// </summary>
        private readonly bool _quoteHyphens;

        /// <summary>
        /// Should use new line separators instead of spaces to separate arguments.
        /// </summary>
        private readonly bool _useNewLineSeparator;

        /// <summary>
        /// Instead of defining which characters must be quoted, define 
        /// which characters we know its safe to not quote. This way leads
        /// to more false-positives (which still work, but don't look as 
        /// nice coming out of the logger), but is less likely to leave a 
        /// security hole.
        /// </summary>
        private Regex _allowedUnquoted;

        /// <summary>
        /// Also, define the characters that we know for certain need quotes.
        /// This is partly to document which characters we know can cause trouble
        /// and partly as a sanity check against a bug creeping in.
        /// </summary>
        private Regex _definitelyNeedQuotes;

        /// <summary>
        /// Use a private property so that we can lazy initialize the regex
        /// </summary>
        private Regex DefinitelyNeedQuotes => _definitelyNeedQuotes
            ?? (_definitelyNeedQuotes = new Regex(_quoteHyphens ? s_definitelyNeedQuotesRegexWithHyphen : s_definitelyNeedQuotesRegexNoHyphen, RegexOptions.None));

        /// <summary>
        /// Use a private getter property to we can lazy initialize the regex
        /// </summary>
        private Regex AllowedUnquoted => _allowedUnquoted
            ?? (_allowedUnquoted = new Regex(_quoteHyphens ? s_allowedUnquotedRegexNoHyphen : s_allowedUnquotedRegexWithHyphen, RegexOptions.IgnoreCase));

        /// <summary>
        /// Checks the given switch parameter to see if it must/can be quoted.
        /// </summary>
        /// <param name="parameter">the string to examine for characters that require quoting</param>
        /// <returns>true, if parameter should be quoted</returns>
        protected virtual bool IsQuotingRequired(string parameter)
        {
            bool isQuotingRequired = false;

            if (parameter != null)
            {
                #region Security Note: About cross-parameter injection
                /*
                        If string parameters have whitespace in them, then a possible attack would
                        be like the following:

                            <Win32Icon>MyFile.ico /out:c:\windows\system32\notepad.exe</Win32Icon>

                            <Csc
                                Win32Icon="$(Win32Icon)"
                                ...
                            />

                        Since we just build up a command-line to pass into CSC.EXE, without quoting,
                        the project might overwrite notepad.exe.

                        If there are spaces in the parameter, then we must quote that parameter.
                    */
                #endregion
                bool hasAllUnquotedCharacters = AllowedUnquoted.IsMatch(parameter);
                bool hasSomeQuotedCharacters = DefinitelyNeedQuotes.IsMatch(parameter);

                isQuotingRequired = !hasAllUnquotedCharacters;
                isQuotingRequired = isQuotingRequired || hasSomeQuotedCharacters;

                Debug.Assert(!hasAllUnquotedCharacters || !hasSomeQuotedCharacters,
                    "At least one of allowedUnquoted or definitelyNeedQuotes is wrong.");
            }

            return isQuotingRequired;
        }

        /// <summary>
        /// Add a space or newline to the specified string if and only if it's not empty.
        /// </summary>
        /// <remarks>
        /// This is a pretty obscure method and so it's only available to inherited classes.
        /// </remarks>
        protected void AppendSpaceIfNotEmpty()
        {
            if (CommandLine.Length != 0)
            {
                if (_useNewLineSeparator)
                {
                    CommandLine.Append(Environment.NewLine);
                }
                else if(CommandLine[CommandLine.Length - 1] != ' ')
                {
                    CommandLine.Append(" ");
                }
            }
        }

        #endregion

        #region Methods for use in inherited classes, do not prepend a space before doing their thing

        /// <summary>
        /// Appends a string. Quotes are added if they are needed.
        /// This method does not append a space to the command line before executing.
        /// </summary>
        /// <remarks>
        /// Escapes any double quotes in the string.
        /// </remarks>
        /// <param name="textToAppend">The string to append</param>
        protected void AppendTextWithQuoting(string textToAppend) => AppendQuotedTextToBuffer(CommandLine, textToAppend);

        /// <summary>
        /// Appends given text to the buffer after first quoting the text if necessary.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="unquotedTextToAppend"></param>
        protected void AppendQuotedTextToBuffer(StringBuilder buffer, string unquotedTextToAppend)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buffer, nameof(buffer));

            if (unquotedTextToAppend != null)
            {
                bool addQuotes = IsQuotingRequired(unquotedTextToAppend);

                if (addQuotes)
                {
                    buffer.Append('"');
                }

                // Count the number of quotes
                int literalQuotes = 0;
                for (int i = 0; i < unquotedTextToAppend.Length; i++)
                {
                    if (unquotedTextToAppend[i] == '"')
                    {
                        literalQuotes++;
                    }
                }
                if (literalQuotes > 0)
                {
                    // Replace any \" sequences with \\"
                    unquotedTextToAppend = unquotedTextToAppend.Replace("\\\"", "\\\\\"");
                    // Now replace any " with \"
                    unquotedTextToAppend = unquotedTextToAppend.Replace("\"", "\\\"");
                }

                buffer.Append(unquotedTextToAppend);

                // Be careful any trailing slash doesn't escape the quote we're about to add
                if (addQuotes && unquotedTextToAppend.EndsWith("\\", StringComparison.Ordinal))
                {
                    buffer.Append('\\');
                }

                if (addQuotes)
                {
                    buffer.Append('"');
                }
            }
        }

        /// <summary>
        /// Appends a string. No quotes are added.
        /// This method does not append a space to the command line before executing.
        /// </summary>
        /// <example>
        /// AppendTextUnquoted(@"Folder name\filename.cs") => "Folder name\\filename.cs"
        /// </example>
        /// <remarks>
        /// In the future, this function may fixup 'textToAppend' to handle
        /// literal embedded quotes.
        /// </remarks>
        /// <param name="textToAppend">The string to append</param>
        public void AppendTextUnquoted(string textToAppend)
        {
            if (textToAppend != null)
            {
                CommandLine.Append(textToAppend);
            }
        }

        /// <summary>
        /// Appends a file name. Quotes are added if they are needed. 
        /// If the first character of the file name is a dash, ".\" is prepended to avoid confusing the file name with a switch
        /// This method does not append a space to the command line before executing.
        /// </summary>
        /// <example>
        /// AppendFileNameWithQuoting("-StrangeFileName.cs") => ".\-StrangeFileName.cs"
        /// </example>
        /// <remarks>
        /// In the future, this function may fixup 'text' to handle
        /// literal embedded quotes.
        /// </remarks>
        /// <param name="fileName">The file name to append</param>
        protected void AppendFileNameWithQuoting(string fileName)
        {
            if (fileName != null)
            {
                // Don't let injection attackers escape from our quotes by sticking in
                // their own quotes. Quotes are illegal.
                VerifyThrowNoEmbeddedDoubleQuotes(string.Empty, fileName);

                fileName = FileUtilities.FixFilePath(fileName);
                if (fileName.Length != 0 && fileName[0] == '-')
                {
                    AppendTextWithQuoting("." + Path.DirectorySeparatorChar + fileName);
                }
                else
                {
                    AppendTextWithQuoting(fileName);
                }
            }
        }

        #endregion

        #region Appending file names

        /// <summary>
        /// Appends a file name quoting it if necessary.
        /// This method appends a space to the command line (if it's not currently empty) before the file name.
        /// </summary>
        /// <example>
        /// AppendFileNameIfNotNull("-StrangeFileName.cs") => ".\-StrangeFileName.cs"
        /// </example>
        /// <param name="fileName">File name to append, if it's null this method has no effect</param>
        public void AppendFileNameIfNotNull(string fileName)
        {
            if (fileName != null)
            {
                // Don't let injection attackers escape from our quotes by sticking in
                // their own quotes. Quotes are illegal.
                VerifyThrowNoEmbeddedDoubleQuotes(string.Empty, fileName);

                AppendSpaceIfNotEmpty();
                AppendFileNameWithQuoting(fileName);
            }
        }

        /// <summary>
        /// Appends a file name quoting it if necessary.
        /// This method appends a space to the command line (if it's not currently empty) before the file name.
        /// </summary>
        /// <example>
        /// See the string overload version
        /// </example>
        /// <param name="fileItem">File name to append, if it's null this method has no effect</param>
        public void AppendFileNameIfNotNull(ITaskItem fileItem)
        {
            if (fileItem != null)
            {
                // Don't let injection attackers escape from our quotes by sticking in
                // their own quotes. Quotes are illegal.
                VerifyThrowNoEmbeddedDoubleQuotes(string.Empty, fileItem.ItemSpec);

                AppendFileNameIfNotNull(fileItem.ItemSpec);
            }
        }

        /// <summary>
        /// Appends array of file name strings, quoting them if necessary, delimited by a delimiter.
        /// This method appends a space to the command line (if it's not currently empty) before the file names.
        /// </summary>
        /// <example>
        /// AppendFileNamesIfNotNull(new string[] {"Alpha.cs", "Beta.cs"}, ",") => "Alpha.cs,Beta.cs"
        /// </example>
        /// <param name="fileNames">File names to append, if it's null this method has no effect</param>
        /// <param name="delimiter">The delimiter between file names</param>
        public void AppendFileNamesIfNotNull(string[] fileNames, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (fileNames != null && fileNames.Length > 0)
            {
                // Don't let injection attackers escape from our quotes by sticking in
                // their own quotes. Quotes are illegal.
                for (int i = 0; i < fileNames.Length; ++i)
                {
                    VerifyThrowNoEmbeddedDoubleQuotes(string.Empty, fileNames[i]);
                }

                AppendSpaceIfNotEmpty();
                for (int i = 0; i < fileNames.Length; ++i)
                {
                    if (i != 0)
                    {
                        AppendTextUnquoted(delimiter);
                    }

                    AppendFileNameWithQuoting(fileNames[i]);
                }
            }
        }

        /// <summary>
        /// Appends array of ITaskItem specs as file names, quoting them if necessary, delimited by a delimiter.
        /// This method appends a space to the command line (if it's not currently empty) before the file names.
        /// </summary>
        /// <example>
        /// See the string[] overload version
        /// </example>
        /// <param name="fileItems">Task items to append, if null this method has no effect</param>
        /// <param name="delimiter">Delimiter to put between items in the command line</param>
        public void AppendFileNamesIfNotNull(ITaskItem[] fileItems, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (fileItems != null && fileItems.Length > 0)
            {
                // Don't let injection attackers escape from our quotes by sticking in
                // their own quotes. Quotes are illegal.
                for (int i = 0; i < fileItems.Length; ++i)
                {
                    if (fileItems[i] != null)
                    {
                        VerifyThrowNoEmbeddedDoubleQuotes(string.Empty, fileItems[i].ItemSpec);
                    }
                }

                AppendSpaceIfNotEmpty();
                for (int i = 0; i < fileItems.Length; ++i)
                {
                    if (i != 0)
                    {
                        AppendTextUnquoted(delimiter);
                    }

                    if (fileItems[i] != null)
                    {
                        AppendFileNameWithQuoting(fileItems[i].ItemSpec);
                    }
                }
            }
        }

        #endregion

        #region Appending switches with quoted parameters

        /// <summary>
        /// Appends a command-line switch that has no separate value, without any quoting.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// AppendSwitch("/utf8output") => "/utf8output"
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        public void AppendSwitch(string switchName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));

            AppendSpaceIfNotEmpty();
            AppendTextUnquoted(switchName);
        }

        /// <summary>
        /// Appends a command-line switch that takes a single string parameter, quoting the parameter if necessary.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// AppendSwitchIfNotNull("/source:", "File Name.cs") => "/source:\"File Name.cs\""
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameter">Switch parameter to append, quoted if necessary. If null, this method has no effect.</param>
        public void AppendSwitchIfNotNull(string switchName, string parameter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));

            if (parameter != null)
            {
                // Now, stick the parameter in.
                AppendSwitch(switchName);
                AppendTextWithQuoting(parameter);
            }
        }

        /// <summary>
        /// Throws if the parameter has a double-quote in it. This is used to prevent parameter
        /// injection. It's virtual so that tools can override this method if they want to have quotes escaped in filenames
        /// </summary>
        /// <param name="switchName">Switch name for error message</param>
        /// <param name="parameter">Switch parameter to scan</param>
        protected virtual void VerifyThrowNoEmbeddedDoubleQuotes(string switchName, string parameter)
        {
            if (parameter != null)
            {
                if (string.IsNullOrEmpty(switchName))
                {
                    ErrorUtilities.VerifyThrowArgument
                        (
                            -1 == parameter.IndexOf('"'),
                            "General.QuotesNotAllowedInThisKindOfTaskParameterNoSwitchName",
                            parameter
                        );
                }
                else
                {
                    ErrorUtilities.VerifyThrowArgument
                        (
                            -1 == parameter.IndexOf('"'),
                            "General.QuotesNotAllowedInThisKindOfTaskParameter",
                            switchName,
                            parameter
                        );
                }
            }
        }

        /// <summary>
        /// Append a switch [overload]
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// See the string overload version
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameter">Switch parameter to append, quoted if necessary. If null, this method has no effect.</param>
        public void AppendSwitchIfNotNull(string switchName, ITaskItem parameter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));

            if (parameter != null)
            {
                AppendSwitchIfNotNull(switchName, parameter.ItemSpec);
            }
        }

        /// <summary>
        /// Appends a command-line switch that takes a string[] parameter,
        /// and add double-quotes around the individual filenames if necessary.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// AppendSwitchIfNotNull("/sources:", new string[] {"Alpha.cs", "Be ta.cs"}, ";") => "/sources:Alpha.cs;\"Be ta.cs\""
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameters">Switch parameters to append, quoted if necessary. If null, this method has no effect.</param>
        /// <param name="delimiter">Delimiter to put between individual parameters, may not be null (may be empty)</param>
        public void AppendSwitchIfNotNull(string switchName, string[] parameters, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (parameters != null && parameters.Length > 0)
            {
                AppendSwitch(switchName);
                bool first = true;
                foreach (string parameter in parameters)
                {
                    if (!first)
                    {
                        AppendTextUnquoted(delimiter);
                    }
                    first = false;
                    AppendTextWithQuoting(parameter);
                }
            }
        }

        /// <summary>
        /// Appends a command-line switch that takes a ITaskItem[] parameter,
        /// and add double-quotes around the individual filenames if necessary.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// See the string[] overload version
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameters">Switch parameters to append, quoted if necessary. If null, this method has no effect.</param>
        /// <param name="delimiter">Delimiter to put between individual parameters, may not be null (may be empty)</param>
        public void AppendSwitchIfNotNull(string switchName, ITaskItem[] parameters, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (parameters != null && parameters.Length > 0)
            {
                AppendSwitch(switchName);
                bool first = true;
                foreach (ITaskItem parameter in parameters)
                {
                    if (!first)
                    {
                        AppendTextUnquoted(delimiter);
                    }
                    first = false;

                    if (parameter != null)
                    {
                        AppendTextWithQuoting(parameter.ItemSpec);
                    }
                }
            }
        }

        #endregion

        #region Append switches with unquoted parameters

        /// <summary>
        /// Appends the literal parameter without trying to quote.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// AppendSwitchUnquotedIfNotNull("/source:", "File Name.cs") => "/source:File Name.cs"
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameter">Switch parameter to append, not quoted. If null, this method has no effect.</param>
        public void AppendSwitchUnquotedIfNotNull(string switchName, string parameter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));

            if (parameter != null)
            {
                // Now, stick the parameter in.
                AppendSwitch(switchName);
                AppendTextUnquoted(parameter);
            }
        }

        /// <summary>
        /// Appends the literal parameter without trying to quote.
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// See the string overload version
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameter">Switch parameter to append, not quoted. If null, this method has no effect.</param>
        public void AppendSwitchUnquotedIfNotNull(string switchName, ITaskItem parameter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));

            if (parameter != null)
            {
                AppendSwitchUnquotedIfNotNull(switchName, parameter.ItemSpec);
            }
        }

        /// <summary>
        /// Appends a command-line switch that takes a string[] parameter, not quoting the individual parameters
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// AppendSwitchUnquotedIfNotNull("/sources:", new string[] {"Alpha.cs", "Be ta.cs"}, ";") => "/sources:Alpha.cs;Be ta.cs"
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameters">Switch parameters to append, not quoted. If null, this method has no effect.</param>
        /// <param name="delimiter">Delimiter to put between individual parameters, may not be null (may be empty)</param>
        public void AppendSwitchUnquotedIfNotNull(string switchName, string[] parameters, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (parameters != null && parameters.Length > 0)
            {
                AppendSwitch(switchName);
                bool first = true;
                foreach (string parameter in parameters)
                {
                    if (!first)
                    {
                        AppendTextUnquoted(delimiter);
                    }
                    first = false;
                    AppendTextUnquoted(parameter);
                }
            }
        }

        /// <summary>
        /// Appends a command-line switch that takes a ITaskItem[] parameter, not quoting the individual parameters
        /// This method appends a space to the command line (if it's not currently empty) before the switch.
        /// </summary>
        /// <example>
        /// See the string[] overload version
        /// </example>
        /// <param name="switchName">The switch to append to the command line, may not be null</param>
        /// <param name="parameters">Switch parameters to append, not quoted. If null, this method has no effect.</param>
        /// <param name="delimiter">Delimiter to put between individual parameters, may not be null (may be empty)</param>
        public void AppendSwitchUnquotedIfNotNull(string switchName, ITaskItem[] parameters, string delimiter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(switchName, nameof(switchName));
            ErrorUtilities.VerifyThrowArgumentNull(delimiter, nameof(delimiter));

            if (parameters != null && parameters.Length > 0)
            {
                AppendSwitch(switchName);
                bool first = true;
                foreach (ITaskItem parameter in parameters)
                {
                    if (!first)
                    {
                        AppendTextUnquoted(delimiter);
                    }
                    first = false;

                    if (parameter != null)
                    {
                        AppendTextUnquoted(parameter.ItemSpec);
                    }
                }
            }
        }

        #endregion
    }
}
