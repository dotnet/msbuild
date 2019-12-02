using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace EndToEnd
{
    public class GivenDotnetUsesDotnetTools : TestBase
    {
        [Fact]
        public void ThenOneDotnetToolsCanBeCalled()
        {
            new DotnetCommand()
                .ExecuteWithCapturedOutput("dev-certs --help")
                    .Should().Pass();
        }
    }
}
