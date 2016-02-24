// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Test
{
    public class VersionCheckMessageHandler : IDotnetTestMessageHandler
    {
        private const int SupportedVersion = 1;

        private readonly IReportingChannel _adapterChannel;

        public VersionCheckMessageHandler(IReportingChannel adapterChannel)
        {
            _adapterChannel = adapterChannel;
        }

        public DotnetTestState HandleMessage(IDotnetTest dotnetTest, Message message)
        {
            var nextState = DotnetTestState.NoOp;
            if (CanHandleMessage(dotnetTest, message))
            {
                HandleMessage(message);
                nextState = DotnetTestState.VersionCheckCompleted;
            }

            return nextState;
        }

        private void HandleMessage(Message message)
        {
            var version = message.Payload?.ToObject<ProtocolVersionMessage>().Version;
            TestHostTracing.Source.TraceInformation(
                "[ReportingChannel]: Requested Version: {0} - Using Version: {1}",
                version,
                SupportedVersion);

            _adapterChannel.Send(new Message
            {
                MessageType = TestMessageTypes.VersionCheck,
                Payload = JToken.FromObject(new ProtocolVersionMessage
                {
                    Version = SupportedVersion,
                }),
            });
        }

        private static bool CanHandleMessage(IDotnetTest dotnetTest, Message message)
        {
            return dotnetTest.State == DotnetTestState.InitialState &&
                TestMessageTypes.VersionCheck.Equals(message.MessageType);
        }
    }
}
