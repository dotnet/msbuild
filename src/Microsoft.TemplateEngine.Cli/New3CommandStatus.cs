// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal enum New3CommandStatus
    {
        /// <summary>
        /// The template was instantiated successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The template instantiation failed.
        /// </summary>
        CreateFailed = unchecked((int)0x80020009),

        /// <summary>
        /// The mandatory parameters for template are missing.
        /// </summary>
        MissingMandatoryParam = unchecked((int)0x8002000F),

        /// <summary>
        /// The values passed for template parameters are invalid.
        /// </summary>
        InvalidParamValues = unchecked((int)0x80020005),

        /// <summary>
        /// The subcommand to run is not specified.
        /// </summary>
        OperationNotSpecified = unchecked((int)0x8002000E),

        /// <summary>
        /// The template is not found.
        /// </summary>
        NotFound = unchecked((int)0x800200006),

        /// <summary>
        /// The operation is cancelled.
        /// </summary>
        Cancelled = unchecked((int)0x80004004),

        /// <summary>
        /// The result received from template engine core is not expected.
        /// </summary>
        UnexpectedResult = unchecked((int)0x80010001),

        /// <summary>
        /// The manipulation with alias has failed.
        /// </summary>
        AliasFailed = unchecked((int)0x80010002),

        /// <summary>
        /// The operation is cancelled due to destructive changes to existing files are detected.
        /// </summary>
        DestructiveChangesDetected = unchecked((int)0x8002000D),

        /// <summary>
        /// Post action failed.
        /// </summary>
        PostActionFailed = unchecked((int)0x80010003),

        /// <summary>
        /// Generic error when displaying help.
        /// </summary>
        DisplayHelpFailed = unchecked((int)0x80010004)
    }
}
