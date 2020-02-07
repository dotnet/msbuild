using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace EndToEnd
{
    public class GivenDotnetUsesDotnetTools : SdkTest
    {
        public GivenDotnetUsesDotnetTools(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ThenOneDotnetToolsCanBeCalled()
        {
            new DotnetCommand(Log)
                .Execute("dev-certs", "--help")
                    .Should().Pass();
        }
    }
}
