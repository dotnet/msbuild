// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Commands;

internal sealed class RedactBinlogCommandArgs
{
    public RedactBinlogCommandArgs(
        string[]? passwordsToRedact,
        string? inputFileName,
        string? outputFileName,
        bool? dryRun,
        bool? overWrite,
        bool? logDetectedSecrets)
    {
        PasswordsToRedact = passwordsToRedact;
        InputFileName = inputFileName;
        OutputFileName = outputFileName;
        DryRun = dryRun;
        OverWrite = overWrite;
        LogDetectedSecrets = logDetectedSecrets;
    }

    public string[]? PasswordsToRedact { get; init; }
    public string? InputFileName { get; init; }
    public string? OutputFileName { get; init; }
    public bool? DryRun { get; init; }
    public bool? OverWrite { get; init; }
    public bool? LogDetectedSecrets { get; init; }
}

