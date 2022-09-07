// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
