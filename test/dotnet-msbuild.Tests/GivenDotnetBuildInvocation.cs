using Microsoft.DotNet.Tools.Build;
using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;
using System.IO;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetBuildInvocation
    {
        [Fact]
        public void WhenNoArgsArePassedThenMsbuildInvocationIsCorrect()
        {
            var msbuildPath = Path.Combine(Directory.GetCurrentDirectory(), "MSBuild.dll");
            BuildCommand.FromArgs()
                .GetProcessStartInfo().Arguments.Should().Be($@"exec {msbuildPath} /m /v:m /t:Build /clp:Summary");
        }
    }
}
