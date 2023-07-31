// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Reporting;

internal interface IStdStreamWriter : IDisposable
{
    void Write(string message);
    void WriteLine(string message);
    void WriteLine();
}

internal interface IStderrWriter : IStdStreamWriter
{ }

internal interface IStdoutWriter : IStdStreamWriter
{ }
