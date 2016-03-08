// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class GlobalErrorMessenger : Messenger<ProjectSnapshot>
    {
        public GlobalErrorMessenger(Action<string, object> transmit)
            : base(MessageTypes.Error, transmit)
        { }

        protected override bool CheckDifference(ProjectSnapshot local, ProjectSnapshot remote)
        {
            return remote != null && Equals(local.GlobalErrorMessage, remote.GlobalErrorMessage);
        }

        protected override void SendPayload(ProjectSnapshot local, Action<object> send)
        {
            if (local.GlobalErrorMessage != null)
            {
                send(local.GlobalErrorMessage);
            }
        }

        protected override void SetValue(ProjectSnapshot local, ProjectSnapshot remote)
        {
            remote.GlobalErrorMessage = local.GlobalErrorMessage;
        }
    }
}
