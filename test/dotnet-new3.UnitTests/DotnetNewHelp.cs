// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public partial class DotnetNewHelp : IClassFixture<SharedHomeDirectory>
    {
        private readonly ITestOutputHelper _log;
        private readonly SharedHomeDirectory _fixture;

        public DotnetNewHelp(SharedHomeDirectory fixture, ITestOutputHelper log)
        {
            _log = log;
            _fixture = fixture;
        }
        
        [Fact]
        public void WontShowLanguageHintInCaseOfOneLang()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "globaljson", "--help")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute()
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("To see help for other template languages");
        }
    }
}
