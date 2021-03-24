using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class FirstRunTest
    {
        private readonly ITestOutputHelper _log;

        public FirstRunTest(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void FirstRunSuccess()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Getting ready")
                .And.NotHaveStdOutContaining("Error");

            new DotnetNewCommand(_log, "--list")
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("classlib");
        }
    }
}
