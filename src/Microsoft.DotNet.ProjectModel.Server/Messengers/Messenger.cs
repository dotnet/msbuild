// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal abstract class Messenger<T> where T : class
    {
        protected readonly Action<string, object> _transmit;

        public Messenger(string messageType, Action<string, object> transmit)
        {
            _transmit = transmit;

            MessageType = messageType;
        }

        public string MessageType { get; }

        public void UpdateRemote(T local, T remote)
        {
            if (!CheckDifference(local, remote))
            {
                var payload = CreatePayload(local);

                _transmit(MessageType, payload);

                SetValue(local, remote);
            }
        }

        protected abstract void SetValue(T local, T remote);
        protected abstract object CreatePayload(T local);
        protected abstract bool CheckDifference(T local, T remote);
    }
}
