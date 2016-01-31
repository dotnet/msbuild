// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class ProjectDiagnosticsMessenger : Messenger<ProjectSnapshot>
    {
        public ProjectDiagnosticsMessenger(Action<string, object> transmit)
            : base(MessageTypes.Diagnostics, transmit)
        { }

        protected override bool CheckDifference(ProjectSnapshot local, ProjectSnapshot remote)
        {
            return remote.ProjectDiagnostics != null &&
                   Enumerable.SequenceEqual(local.ProjectDiagnostics, remote.ProjectDiagnostics);
        }

        protected override object CreatePayload(ProjectSnapshot local)
        {
            return new DiagnosticsListMessage(local.ProjectDiagnostics);
        }

        protected override void SetValue(ProjectSnapshot local, ProjectSnapshot remote)
        {
            remote.ProjectDiagnostics = local.ProjectDiagnostics;
        }
    }
}
