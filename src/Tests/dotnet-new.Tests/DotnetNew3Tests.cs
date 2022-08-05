// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public class DotnetNewTests : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void CanDisableBuiltInTemplates_List()
        {
            var commandResult = new DotnetNewCommand(_log, "list", "--debug:disable-sdk-templates")
                .WithCustomHive(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("console")
                .And.HaveStdOutContaining("No templates installed.");
        }

        [Fact]
        public void CanDisableBuiltInTemplates_Template()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--debug:disable-sdk-templates")
                .WithCustomHive(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdErrContaining("No templates found matching: 'console'.");
        }

    }
}
