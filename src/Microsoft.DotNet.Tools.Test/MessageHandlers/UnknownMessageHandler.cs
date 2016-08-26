// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public class UnknownMessageHandler : IDotnetTestMessageHandler
    {
        private readonly IReportingChannel _adapterChannel;

        public UnknownMessageHandler(IReportingChannel adapterChannel)
        {
            _adapterChannel = adapterChannel;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var error = $"No handler for message '{message.MessageType}' when at state '{dotnetTest.State}'";

            TestHostTracing.Source.TraceEvent(TraceEventType.Error, 0, error);

            _adapterChannel.SendError(error);

            throw new InvalidOperationException(error);
        }
    }
}
