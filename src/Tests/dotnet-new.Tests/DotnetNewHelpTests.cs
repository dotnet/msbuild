// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewHelpTests : IClassFixture<SharedHomeDirectory>
    {
        private readonly ITestOutputHelper _log;
        private readonly SharedHomeDirectory _fixture;

        public DotnetNewHelpTests(SharedHomeDirectory fixture, ITestOutputHelper log) : base(log)
        {
            _log = log;
            _fixture = fixture;
        }

        [Fact]
        public void WontShowLanguageHintInCaseOfOneLang()
        {
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "globaljson", "--help")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute()
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContaining("global.json file")
                    .And.NotHaveStdOutContaining("To see help for other template languages");
        }
    }
}
