// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    public class ProjectInstance_FromImmutableProjectLink_Tests
    {
        /// <summary>
        /// Ensures that a ProjectInstance can be created without accessing lazy properties from an immutable project link.
        /// </summary>
        [Fact]
        public void ProjectInstanceAccessMinimalState()
        {
            var projectLink = new FakeProjectLink(@"Q:\FakeFolder\Project\Project.proj");
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
        }
    }
}
