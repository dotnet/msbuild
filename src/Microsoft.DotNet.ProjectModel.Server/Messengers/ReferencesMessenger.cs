// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class ReferencesMessenger : Messenger<ProjectContextSnapshot>
    {
        public ReferencesMessenger(Action<string, object> transmit)
            : base(MessageTypes.References, transmit)
        { }

        protected override bool CheckDifference(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            return remote.FileReferences != null &&
                   remote.ProjectReferences != null &&
                   Enumerable.SequenceEqual(local.FileReferences, remote.FileReferences) &&
                   Enumerable.SequenceEqual(local.ProjectReferences, remote.ProjectReferences);
        }

        protected override object CreatePayload(ProjectContextSnapshot local)
        {
            return new ReferencesMessage
            {
                Framework = local.TargetFramework.ToPayload(),
                ProjectReferences = local.ProjectReferences,
                FileReferences = local.FileReferences
            };
        }

        protected override void SetValue(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            remote.FileReferences = local.FileReferences;
            remote.ProjectReferences = local.ProjectReferences;
        }

        private class ReferencesMessage
        {
            public FrameworkData Framework { get; set; }
            public IReadOnlyList<string> FileReferences { get; set; }
            public IReadOnlyList<ProjectReferenceDescription> ProjectReferences { get; set; }
        }
    }
}
