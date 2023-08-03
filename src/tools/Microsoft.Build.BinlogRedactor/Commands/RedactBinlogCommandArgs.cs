// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Commands;

internal sealed class RedactBinlogCommandArgs
{
    public RedactBinlogCommandArgs(
        string[]? passwordsToRedact,
        string? inputPath,
        string? outputFileName,
        bool? dryRun,
        bool? overWrite,
        bool? recurse,
        bool? logDetectedSecrets)
    {
        PasswordsToRedact = passwordsToRedact;
        InputPath = inputPath;
        OutputFileName = outputFileName;
        DryRun = dryRun;
        OverWrite = overWrite;
        Recurse = recurse;
        LogDetectedSecrets = logDetectedSecrets;
    }

    public string[]? PasswordsToRedact { get; init; }
    public string? InputPath { get; init; }
    public string? OutputFileName { get; init; }
    public bool? DryRun { get; init; }
    public bool? OverWrite { get; init; }
    public bool? Recurse { get; init; }
    public bool? LogDetectedSecrets { get; init; }
}

