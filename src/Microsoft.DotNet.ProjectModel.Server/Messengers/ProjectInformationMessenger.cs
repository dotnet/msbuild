// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class ProjectInformationMessenger : Messenger<Snapshot>
    {
        public ProjectInformationMessenger(Action<string, object> transmit)
            : base(MessageTypes.ProjectInformation, transmit)
        { }

        protected override bool CheckDifference(Snapshot local, Snapshot remote)
        {
            return remote.Project != null &&
                   string.Equals(local.Project.Name, remote.Project.Name) &&
                   string.Equals(local.GlobalJsonPath, remote.GlobalJsonPath) &&
                   Enumerable.SequenceEqual(local.Project.GetTargetFrameworks().Select(f => f.FrameworkName),
                                           remote.Project.GetTargetFrameworks().Select(f => f.FrameworkName)) &&
                   Enumerable.SequenceEqual(local.Project.GetConfigurations(), remote.Project.GetConfigurations()) &&
                   Enumerable.SequenceEqual(local.Project.Commands, remote.Project.Commands) &&
                   Enumerable.SequenceEqual(local.ProjectSearchPaths, remote.ProjectSearchPaths);
        }

        protected override object CreatePayload(Snapshot local)
        {
            return new ProjectInformation(local.Project, local.GlobalJsonPath, local.ProjectSearchPaths, _resolver);
        }

        protected override void SetValue(Snapshot local, Snapshot remote)
        {
            remote.Project = local.Project;
            remote.GlobalJsonPath = local.GlobalJsonPath;
            remote.ProjectSearchPaths = local.ProjectSearchPaths;
        }

        private class ProjectInformation
        {
            public ProjectInformation(Project project,
                                      string gloablJsonPath,
                                      IEnumerable<string> projectSearchPath,
                                      FrameworkReferenceResolver resolver)
            {
                Name = project.Name;
                Frameworks = project.GetTargetFrameworks().Select(f => f.FrameworkName.ToPayload(resolver)).ToList();
                Configurations = project.GetConfigurations().ToList();
                Commands = project.Commands;
                ProjectSearchPaths = new List<string>(projectSearchPath);
                GlobalJsonPath = gloablJsonPath;
            }

            public string Name { get; }

            public IReadOnlyList<FrameworkData> Frameworks { get; }

            public IReadOnlyList<string> Configurations { get; }

            public IDictionary<string, string> Commands { get; }

            public IReadOnlyList<string> ProjectSearchPaths { get; }

            public string GlobalJsonPath { get; }
        }
    }
}
