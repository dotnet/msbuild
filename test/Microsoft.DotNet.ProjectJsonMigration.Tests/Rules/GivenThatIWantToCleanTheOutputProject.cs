// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Internal.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToCleanTheOutputProject : TestBase
    {
        [Fact]
        public void ItRemovesEmptyTargetsFromTheProject()
        {
            var mockProj = ProjectRootElement.Create();
            var target = mockProj.CreateTargetElement("Test");
            mockProj.AppendChild(target);
            target.AddTask("Exec");

            var targetToRemove = mockProj.CreateTargetElement("ToRemove");
            mockProj.AppendChild(targetToRemove);

            var migrationRuleInputs = new MigrationRuleInputs(Enumerable.Empty<ProjectContext>(), mockProj, null, null);
            var cleanOutputProjectRule = new CleanOutputProjectRule();

            cleanOutputProjectRule.Apply(null, migrationRuleInputs);

            mockProj.Targets.Should().HaveCount(c => c == 1);
        }
    }
}