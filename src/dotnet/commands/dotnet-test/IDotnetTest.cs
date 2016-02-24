// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Tools.Test
{
    public interface IDotnetTest : IDisposable
    {
        string PathToAssemblyUnderTest { get; }

        DotnetTestState State { get; }

        DotnetTest AddMessageHandler(IDotnetTestMessageHandler messageHandler);

        IDotnetTestMessageHandler TestSessionTerminateMessageHandler { set; }

        IDotnetTestMessageHandler UnknownMessageHandler { set; }

        void StartHandlingMessages();

        void StartListeningTo(IReportingChannel reportingChannel);
    }
}
