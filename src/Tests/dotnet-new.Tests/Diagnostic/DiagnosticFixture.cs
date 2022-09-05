// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DiagnosticFixture
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public DiagnosticFixture(IMessageSink sink)
        {
           _diagnosticMessageSink = sink;
        }

        public IMessageSink DiagnosticSink => _diagnosticMessageSink;
    }
}
