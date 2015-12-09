// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.InternalModels;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class DependencyDiagnosticsMessenger : Messenger<ProjectSnapshot>
    {
        public DependencyDiagnosticsMessenger(Action<string, object> transmit)
            : base(MessageTypes.DependencyDiagnostics, transmit)
        { }

        protected override bool CheckDifference(ProjectSnapshot local, ProjectSnapshot remote)
        {
            return remote.DependencyDiagnostics != null &&
                   Enumerable.SequenceEqual(local.DependencyDiagnostics, remote.DependencyDiagnostics);
        }

        protected override object CreatePayload(ProjectSnapshot local)
        {
            return new DiagnosticsListMessage(
                local.DependencyDiagnostics,
                local.TargetFramework?.ToPayload(_resolver));
        }

        protected override void SetValue(ProjectSnapshot local, ProjectSnapshot remote)
        {
            remote.DependencyDiagnostics = local.DependencyDiagnostics;
        }
    }
}
