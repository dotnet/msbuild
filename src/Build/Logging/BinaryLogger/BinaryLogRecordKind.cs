// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Logging
{
    internal enum BinaryLogRecordKind
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
        AssemblyLoad,
    }
}
