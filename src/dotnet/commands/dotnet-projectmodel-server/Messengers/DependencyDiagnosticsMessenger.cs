// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class DependencyDiagnosticsMessenger : Messenger<ProjectContextSnapshot>
    {
        public DependencyDiagnosticsMessenger(Action<string, object> transmit)
            : base(MessageTypes.DependencyDiagnostics, transmit)
        { }

        protected override bool CheckDifference(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            return remote.DependencyDiagnostics != null &&
                   Enumerable.SequenceEqual(local.DependencyDiagnostics, remote.DependencyDiagnostics);
        }

        protected override void SendPayload(ProjectContextSnapshot local, Action<object> send)
        {
            send(new DiagnosticsListMessage(
                local.DependencyDiagnostics,
                local.TargetFramework?.ToPayload()));
        }

        protected override void SetValue(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            remote.DependencyDiagnostics = local.DependencyDiagnostics;
        }
    }
}
