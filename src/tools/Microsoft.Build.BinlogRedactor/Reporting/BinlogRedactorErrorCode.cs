// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Reporting
{
    /// <summary>
    /// BinlogRedactor error codes in thrown <see cref="BinlogRedactorException"/>. Correspond to BinlogRedactor root command exit codes.
    ///
    /// Exit codes based on
    ///  * https://tldp.org/LDP/abs/html/exitcodes.html
    ///  * https://github.com/openbsd/src/blob/master/include/sysexits.h.
    /// related reference: dotnet new exit codes: https://aka.ms/templating-exit-codes.
    /// Future exit codes should be allocated in a range of 107 - 113. If not sufficient, a range of 79 - 99 may be used as well.
    /// </summary>
    internal enum BinlogRedactorErrorCode
    {
        Success = 0,

        /// <summary>
        /// Invalid, corrupted, unexpected data - mostly means corrupted input log.
        /// </summary>
        InvalidData = 65,

        /// <summary>
        /// Unexpected internal error in BinlogRedactor. This might indicate a bug.
        /// </summary>
        InternalError = 70,

        /// <summary>
        /// Destination file already exists and force write is not requested - so command cannot proceed without destructive changes.
        /// </summary>
        FileSystemWriteFailed = 73,

        /// <summary>
        /// A required argument is missing
        /// </summary>
        RequiredOptionMissing = 102,

        /// <summary>
        /// Unrecognized option(s) and/or argument(s) for a command.
        /// </summary>
        InvalidOption = 127,

        /// <summary>
        /// 
        /// </summary>
        OperationTerminatedByUser = 130,

        DotnetCommandError = 107,

        NotEnoughInformationToProceed = 108,

        UnsupportedScenario = 109,

        NotYetImplementedScenario = 110,
    }
}
