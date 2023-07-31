// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Reporting;

internal class DefaultStderrWriter : StdStreamWriterBase, IStderrWriter
{
    protected override TextWriter Writer => Console.Error;
    public override void Dispose() => Writer.Flush();
}
