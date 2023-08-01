// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.Reporting;

internal sealed class DefaultStdoutWriter : StdStreamWriterBase, IStdoutWriter
{
    protected override TextWriter Writer => Console.Out;
    public override void Dispose() => Writer.Flush();
}
