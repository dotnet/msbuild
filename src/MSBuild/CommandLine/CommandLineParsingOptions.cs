// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.CommandLine.Experimental;

/// <summary>
/// Controls which inputs the command-line parser reads and how it handles unknown switches.
/// </summary>
internal sealed class CommandLineParsingOptions
{
    /// <summary>
    /// Gets or sets whether unknown switches cause a <see cref="CommandLineSwitchException"/>.
    /// The default is <see langword="true"/>.
    /// When false, the parser returns the original tokens in <see cref="CommandLineSwitchesAccessor.UnrecognizedArguments"/>.
    /// Other parsing errors still cause exceptions.
    /// </summary>
    public bool ThrowOnUnknownSwitches { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the parser reads explicit and automatic response files.
    /// The default is <see langword="true"/>.
    /// When false, the parser returns explicit response-file tokens in <see cref="CommandLineSwitchesAccessor.UnexpandedResponseFileArguments"/>.
    /// </summary>
    public bool ReadResponseFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the parser includes switches from MSBUILD_LOGGING_ARGS.
    /// The default is <see langword="true"/>.
    /// </summary>
    public bool ReadLoggingArgumentsFromEnvironment { get; set; } = true;
}
