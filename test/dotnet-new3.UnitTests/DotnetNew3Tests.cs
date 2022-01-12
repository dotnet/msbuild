// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using ApprovalTests;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNew3Tests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNew3Tests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanDisableBuiltInTemplates_List()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var commandResult = new DotnetNewCommand(_log, "list", "--debug:disable-sdk-templates")
                .WithCustomHive()
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOutContaining("console")
                .And.HaveStdErrContaining("No templates installed.");
        }

        [Fact]
        public void CanDisableBuiltInTemplates_Template()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var commandResult = new DotnetNewCommand(_log, "console", "--debug:disable-sdk-templates")
                .WithCustomHive()
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdErrContaining("No templates found matching: 'console'.");
        }

    }
}
