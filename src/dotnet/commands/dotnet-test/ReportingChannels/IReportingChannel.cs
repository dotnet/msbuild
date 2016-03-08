// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Testing.Abstractions;

namespace Microsoft.DotNet.Tools.Test
{
    public interface IReportingChannel : IDisposable
    {
        event EventHandler<Message> MessageReceived;

        int Port { get; }

        void Connect();

        void Send(Message message);

        void SendError(string error);

        void SendError(Exception ex);
    }
}
