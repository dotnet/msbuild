// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
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
