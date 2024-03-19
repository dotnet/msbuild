// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Indicates the type of record stored in the binary log.
    /// There is a record type for each type of build event and there
    /// are also few meta-data record types (e.g. string data, lookup data, EOF).
    /// </summary>
    public enum BinaryLogRecordKind
    {
        EndOfFile = 0,
        BuildStarted,
        BuildFinished,
        ProjectStarted,
        ProjectFinished,
        TargetStarted,
        TargetFinished,
        TaskStarted,
        TaskFinished,
        Error,
        Warning,
        Message,
        TaskCommandLine,
        CriticalBuildMessage,
        ProjectEvaluationStarted,
        ProjectEvaluationFinished,
        ProjectImported,
        ProjectImportArchive,
        TargetSkipped,
        PropertyReassignment,
        UninitializedPropertyRead,
        EnvironmentVariableRead,
        PropertyInitialValueSet,
        NameValueList,
        String,
        TaskParameter,
        ResponseFileUsed,
        GeneratedFileUsed,
        AssemblyLoad,
    }
}
