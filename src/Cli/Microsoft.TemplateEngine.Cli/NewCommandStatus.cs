// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// Exit codes based on
    ///  * https://tldp.org/LDP/abs/html/exitcodes.html
    ///  * https://github.com/openbsd/src/blob/master/include/sysexits.h.
    /// Further documentation: https://aka.ms/templating-exit-codes.
    /// Future exit codes should be allocated in a range of 107 - 113. If not sufficient, a range of 79 - 99 may be used as well.
    ///
    /// 127 is not explicitly used here - it is injected from sdk:
    ///    https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Parser.cs#L148.
    /// </summary>
    internal enum NewCommandStatus
    {
        /// <summary>
        /// The template was instantiated successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Unexpected internal software issue. The result received from template engine core is not expected.
        /// </summary>
        Unexpected = 70,

        /// <summary>
        /// Can't create output file. The operation was cancelled due to detection of an attempt to perform destructive changes to existing files.
        /// </summary>
        CannotCreateOutputFile = 73,

        /// <summary>
        /// Instantiation Failed - Processing issues.
        /// </summary>
        CreateFailed = 100,

        /// <summary>
        /// Invalid template or template package.
        /// </summary>
        TemplateIssueDetected = 101,

        /// <summary>
        /// Missing required option(s) and/or argument(s) for the command.
        /// </summary>
        MissingRequiredOption = 102,

        /// <summary>
        /// The template or the template package was not found.
        /// </summary>
        NotFound = 103,

        /// <summary>
        /// The operation was cancelled.
        /// </summary>
        Cancelled = 104,

        /// <summary>
        /// Instantiation Failed - Post action failed.
        /// </summary>
        PostActionFailed = 105,

        /// <summary>
        /// Installation/Uninstallation Failed - Processing issues.
        /// </summary>
        InstallFailed = 106,

        /// <summary>
        /// Unrecognized option(s) and/or argument(s) for a command.
        /// </summary>
        InvalidOption = 127,
    }
}
