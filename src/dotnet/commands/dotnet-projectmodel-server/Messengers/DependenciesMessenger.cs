// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class DependenciesMessenger : Messenger<ProjectContextSnapshot>
    {
        public DependenciesMessenger(Action<string, object> transmit)
            : base(MessageTypes.Dependencies, transmit)
        { }

        protected override bool CheckDifference(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            return remote.Dependencies != null &&
                   string.Equals(local.RootDependency, remote.RootDependency) &&
                   Equals(local.TargetFramework, remote.TargetFramework) &&
                   Enumerable.SequenceEqual(local.Dependencies, remote.Dependencies);
        }

        protected override void SendPayload(ProjectContextSnapshot local, Action<object> send)
        {
            send(new DependenciesMessage
            {
                Framework = local.TargetFramework.ToPayload(),
                RootDependency = local.RootDependency,
                Dependencies = local.Dependencies
            });
        }

        protected override void SetValue(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            remote.Dependencies = local.Dependencies;
        }

        private class DependenciesMessage
        {
            public FrameworkData Framework { get; set; }
            public string RootDependency { get; set; }
            public IDictionary<string, DependencyDescription> Dependencies { get; set; }
        }
    }
}
